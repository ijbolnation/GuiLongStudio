using System;
using System.Buffers.Binary;

namespace AssetStudio
{
    internal enum HdrPixelFormat
    {
        RGBAHalf,
        RGBAFloat,
        RGB9e5,
        RGBFloat,   // 3 x float32, used for faces that were decoded from a compressed format
        BC6H,       // compressed, 1 byte per pixel; only used to work out the face layout
        BC7,        // compressed, 1 byte per pixel; only used to work out the face layout
        RGBA8Srgb,  // 8-bit RGBA holding sRGB-encoded colour (converted to linear when sampled)
        RGBA8Linear, // 8-bit RGBA holding linear colour
    }

    /// <summary>
    /// Resamples the six faces of a cubemap into an equirectangular (lat/long) panorama.
    /// Face order is Unity's: +X, -X, +Y, -Y, +Z, -Z. The panorama centre looks along +Z with +X to the right.
    /// </summary>
    internal static class EquirectRenderer
    {
        private static readonly float[] SrgbToLinear = BuildSrgbTable();

        private static float[] BuildSrgbTable()
        {
            var table = new float[256];
            for (var i = 0; i < 256; i++)
            {
                var c = i / 255.0;
                table[i] = (float)(c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4));
            }
            return table;
        }

        public static int BytesPerPixel(HdrPixelFormat format)
        {
            switch (format)
            {
                case HdrPixelFormat.RGBAHalf: return 8;
                case HdrPixelFormat.RGBAFloat: return 16;
                case HdrPixelFormat.RGBFloat: return 12;
                case HdrPixelFormat.BC6H:
                case HdrPixelFormat.BC7: return 1;
                default: return 4;
            }
        }

        /// <summary>
        /// Works out the byte distance between faces. Without mip maps the faces are simply back to back; with a mip chain
        /// each face is assumed to hold its own full chain (face-major), so the stride is dataLength / 6.
        /// </summary>
        public static bool TryGetFaceStride(int dataLength, int faceSize, HdrPixelFormat format, out int faceStride, out string error)
        {
            faceStride = 0;
            error = null;
            var face0 = (long)faceSize * faceSize * BytesPerPixel(format);
            if (dataLength < face0 * 6)
            {
                error = $"image data ({dataLength} bytes) is smaller than six {faceSize}x{faceSize} faces ({face0 * 6} bytes)";
                return false;
            }
            if (dataLength == face0 * 6)
            {
                faceStride = (int)face0;
                return true;
            }
            if (dataLength % 6 != 0)
            {
                error = $"image data size {dataLength} is not divisible by 6 faces";
                return false;
            }
            faceStride = dataLength / 6;
            return true;
        }

        public static void RenderRow(byte[] data, int faceSize, int faceStride, HdrPixelFormat format, bool flipV,
            int outWidth, int outHeight, int y, float[] rgb)
        {
            var lat = (0.5f - (y + 0.5f) / outHeight) * MathF.PI;
            var cosLat = MathF.Cos(lat);
            var sinLat = MathF.Sin(lat);
            for (var x = 0; x < outWidth; x++)
            {
                var lon = ((x + 0.5f) / outWidth - 0.5f) * 2f * MathF.PI;
                var dx = MathF.Sin(lon) * cosLat;
                var dy = sinLat;
                var dz = MathF.Cos(lon) * cosLat;
                SampleDirection(data, faceSize, faceStride, format, flipV, dx, dy, dz, out var r, out var g, out var b);
                rgb[x * 3] = r;
                rgb[x * 3 + 1] = g;
                rgb[x * 3 + 2] = b;
            }
        }

        public static void DirectionToFace(float x, float y, float z, out int face, out float u, out float v)
        {
            float ax = MathF.Abs(x), ay = MathF.Abs(y), az = MathF.Abs(z);
            float sc, tc, ma;
            // standard OpenGL / Direct3D cube map face table
            if (ax >= ay && ax >= az)
            {
                ma = ax;
                if (x > 0) { face = 0; sc = -z; tc = -y; }
                else { face = 1; sc = z; tc = -y; }
            }
            else if (ay >= az)
            {
                ma = ay;
                if (y > 0) { face = 2; sc = x; tc = z; }
                else { face = 3; sc = x; tc = -z; }
            }
            else
            {
                ma = az;
                if (z > 0) { face = 4; sc = x; tc = -y; }
                else { face = 5; sc = -x; tc = -y; }
            }
            u = 0.5f * (sc / ma + 1f);
            v = 0.5f * (tc / ma + 1f);
        }

        private static void SampleDirection(byte[] data, int size, int faceStride, HdrPixelFormat format, bool flipV,
            float dx, float dy, float dz, out float r, out float g, out float b)
        {
            DirectionToFace(dx, dy, dz, out var face, out var u, out var v);

            // bilinear sample inside the face (no cross-face blending)
            var fx = u * size - 0.5f;
            var fy = v * size - 0.5f;
            var x0 = (int)MathF.Floor(fx);
            var y0 = (int)MathF.Floor(fy);
            var tx = fx - x0;
            var ty = fy - y0;
            var x1 = Math.Clamp(x0 + 1, 0, size - 1);
            var y1 = Math.Clamp(y0 + 1, 0, size - 1);
            x0 = Math.Clamp(x0, 0, size - 1);
            y0 = Math.Clamp(y0, 0, size - 1);

            Fetch(data, size, faceStride, format, flipV, face, x0, y0, out var r00, out var g00, out var b00);
            Fetch(data, size, faceStride, format, flipV, face, x1, y0, out var r10, out var g10, out var b10);
            Fetch(data, size, faceStride, format, flipV, face, x0, y1, out var r01, out var g01, out var b01);
            Fetch(data, size, faceStride, format, flipV, face, x1, y1, out var r11, out var g11, out var b11);

            r = Lerp(Lerp(r00, r10, tx), Lerp(r01, r11, tx), ty);
            g = Lerp(Lerp(g00, g10, tx), Lerp(g01, g11, tx), ty);
            b = Lerp(Lerp(b00, b10, tx), Lerp(b01, b11, tx), ty);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static void Fetch(byte[] data, int size, int faceStride, HdrPixelFormat format, bool flipV,
            int face, int x, int y, out float r, out float g, out float b)
        {
            var row = flipV ? size - 1 - y : y;
            var offset = (long)face * faceStride + ((long)row * size + x) * BytesPerPixel(format);
            var span = new ReadOnlySpan<byte>(data, (int)offset, BytesPerPixel(format));
            switch (format)
            {
                case HdrPixelFormat.RGBAHalf:
                    r = (float)BitConverter.ToHalf(span.Slice(0, 2));
                    g = (float)BitConverter.ToHalf(span.Slice(2, 2));
                    b = (float)BitConverter.ToHalf(span.Slice(4, 2));
                    break;
                case HdrPixelFormat.RGBFloat:
                case HdrPixelFormat.RGBAFloat:
                    r = BitConverter.ToSingle(span.Slice(0, 4));
                    g = BitConverter.ToSingle(span.Slice(4, 4));
                    b = BitConverter.ToSingle(span.Slice(8, 4));
                    break;
                case HdrPixelFormat.RGBA8Srgb:
                    r = SrgbToLinear[span[0]];
                    g = SrgbToLinear[span[1]];
                    b = SrgbToLinear[span[2]];
                    break;
                case HdrPixelFormat.RGBA8Linear:
                    r = span[0] / 255f;
                    g = span[1] / 255f;
                    b = span[2] / 255f;
                    break;
                case HdrPixelFormat.BC6H:
                case HdrPixelFormat.BC7:
                    throw new InvalidOperationException("Compressed faces must be decoded before sampling");
                default:
                    // shared exponent: R bits 0-8, G 9-17, B 18-26, exponent 27-31 (bias 15, 9 mantissa bits)
                    var p = BinaryPrimitives.ReadUInt32LittleEndian(span);
                    var scale = MathF.Pow(2f, (int)(p >> 27) - 24);
                    r = (p & 0x1FF) * scale;
                    g = ((p >> 9) & 0x1FF) * scale;
                    b = ((p >> 18) & 0x1FF) * scale;
                    break;
            }
        }
    }
}
