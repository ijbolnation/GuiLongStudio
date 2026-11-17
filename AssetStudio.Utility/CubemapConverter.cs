using System;
using System.Globalization;
using System.IO;

namespace AssetStudio
{
    public static class CubemapConverter
    {
        /// <summary>
        /// Face rows are read as stored (row 0 = top, the OpenGL/Direct3D cube map convention).
        /// Set to true if exported panoramas come out upside-down.
        /// </summary>
        public static bool FlipFacesVertically = false;

        /// <summary>
        /// 8-bit formats (BC7) are converted from sRGB to linear before they are written to the (linear) EXR.
        /// null = follow the texture's own colour space flag (0 = linear, anything else = sRGB); true/false forces it.
        /// </summary>
        public static bool? TreatLdrAsSrgb = null;

        public static bool IsSupportedFormat(TextureFormat format) => TryMapFormat(format, out _);

        private static bool TryMapFormat(TextureFormat format, out HdrPixelFormat result)
        {
            switch (format)
            {
                case TextureFormat.RGBAHalf: result = HdrPixelFormat.RGBAHalf; return true;
                case TextureFormat.RGBAFloat: result = HdrPixelFormat.RGBAFloat; return true;
                case TextureFormat.RGB9e5Float: result = HdrPixelFormat.RGB9e5; return true;
                case TextureFormat.BC6H: result = HdrPixelFormat.BC6H; return true;
                case TextureFormat.BC7: result = HdrPixelFormat.BC7; return true;
                default: result = default; return false;
            }
        }

        /// <summary>
        /// Writes the cubemap as one equirectangular OpenEXR (HALF RGB, linear, no compression).
        /// Supports RGBAHalf, RGBAFloat, RGB9e5Float, BC6H and BC7 (for compressed formats the top mip of each face is used).
        /// The panorama is 4x faceSize wide and 2x faceSize tall.
        /// </summary>
        public static bool TryExportEquirectExr(Texture2D cubemap, string path, out string error)
        {
            error = null;
            if (!CanExportExr(cubemap, out error))
                return false;
            try
            {
                using (var file = File.Create(path))
                    return TryExportEquirectExr(cubemap, file, out error);
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        /// <summary>Same as the file overload, but writes the EXR into <paramref name="output"/> (e.g. a MemoryStream).</summary>
        public static bool TryExportEquirectExr(Texture2D cubemap, Stream output, out string error)
        {
            error = null;
            if (!TryMapFormat(cubemap.m_TextureFormat, out var format))
            {
                error = $"format {cubemap.m_TextureFormat} is not supported";
                return false;
            }
            var size = cubemap.m_Width;
            if (size <= 0 || cubemap.m_Height != size)
            {
                error = $"faces must be square (got {cubemap.m_Width}x{cubemap.m_Height})";
                return false;
            }
            var data = cubemap.image_data.GetData();
            if (data == null || data.Length == 0)
            {
                error = "no image data";
                return false;
            }
            if ((format == HdrPixelFormat.BC6H || format == HdrPixelFormat.BC7) && size % 4 != 0)
            {
                error = $"{cubemap.m_TextureFormat} faces must be a multiple of 4 pixels wide (got {size})";
                return false;
            }
            if (!EquirectRenderer.TryGetFaceStride(data.Length, size, format, out var faceStride, out error))
                return false;

            Logger.Info($"Cubemap {cubemap.m_Name}: {cubemap.m_TextureFormat}, {size}x{size} faces, {cubemap.m_MipCount} mip(s), " +
                        $"{cubemap.m_ImageCount} image(s), face stride {faceStride} bytes");

            if (format == HdrPixelFormat.BC6H)
            {
                // Decode the top mip of every face to float RGB; each face is followed by its own mip chain
                var facePixels = size * size * 12;
                var decoded = new byte[6 * facePixels];
                var stride = faceStride;
                try
                {
                    System.Threading.Tasks.Parallel.For(0, 6, face =>
                    {
                        var rgb = new float[size * size * 3];
                        Bc6hDecoder.DecodeImage(data, face * stride, size, rgb);
                        Buffer.BlockCopy(rgb, 0, decoded, face * facePixels, facePixels);
                    });
                }
                catch (Exception e)
                {
                    error = e.InnerException?.Message ?? e.Message;
                    return false;
                }
                data = decoded;
                faceStride = facePixels;
                format = HdrPixelFormat.RGBFloat;
            }

            if (format == HdrPixelFormat.BC7)
            {
                // Decode the top mip of every face to 8-bit RGBA; each face is followed by its own mip chain
                var facePixels = size * size * 4;
                var decoded = new byte[6 * facePixels];
                var stride = faceStride;
                try
                {
                    System.Threading.Tasks.Parallel.For(0, 6, face =>
                        Bc7Decoder.DecodeImage(data, face * stride, size, decoded, face * facePixels));
                }
                catch (Exception e)
                {
                    error = e.InnerException?.Message ?? e.Message;
                    return false;
                }
                var srgb = TreatLdrAsSrgb ?? (cubemap.m_ColorSpace != 0);
                Logger.Info($"Cubemap {cubemap.m_Name}: colour space flag {cubemap.m_ColorSpace}, treating as {(srgb ? "sRGB" : "linear")}");
                data = decoded;
                faceStride = facePixels;
                format = srgb ? HdrPixelFormat.RGBA8Srgb : HdrPixelFormat.RGBA8Linear;
            }

            var width = size * 4;
            var height = size * 2;
            var flip = FlipFacesVertically;
            try
            {
                ExrWriter.WriteRgbHalf(output, width, height,
                    (y, rgb) => EquirectRenderer.RenderRow(data, size, faceStride, format, flip, width, height, y, rgb));
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
            return true;
        }

        private static bool CanExportExr(Texture2D cubemap, out string error)
        {
            error = null;
            if (!TryMapFormat(cubemap.m_TextureFormat, out _))
            {
                error = $"format {cubemap.m_TextureFormat} is not supported";
                return false;
            }
            if (cubemap.m_Width <= 0 || cubemap.m_Height != cubemap.m_Width)
            {
                error = $"faces must be square (got {cubemap.m_Width}x{cubemap.m_Height})";
                return false;
            }
            return true;
        }

        /// <summary>Small description written next to a raw (.tex) cubemap dump.</summary>
        public static string GetInfoJson(Texture2D cubemap, int dataSize)
        {
            var name = (cubemap.m_Name ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
            return string.Format(CultureInfo.InvariantCulture,
                "{{\n  \"name\": \"{0}\",\n  \"width\": {1},\n  \"height\": {2},\n  \"format\": \"{3}\",\n  \"mipCount\": {4},\n  \"imageCount\": {5},\n  \"dataSize\": {6}\n}}\n",
                name, cubemap.m_Width, cubemap.m_Height, cubemap.m_TextureFormat, cubemap.m_MipCount, cubemap.m_ImageCount, dataSize);
        }
    }
}
