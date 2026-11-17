using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AssetStudio
{
    /// <summary>
    /// Minimal OpenEXR writer: single-part scanline image, RGB, HALF channels, no compression.
    /// Rows are produced on demand so large images never need to be held in memory.
    /// </summary>
    internal static class ExrWriter
    {
        private const int RowsPerChunk = 32;

        /// <param name="fillRow">Fills <c>rgb</c> (length width*3, interleaved R,G,B) for row <c>y</c> (0 = top).</param>
        public static void WriteRgbHalf(Stream stream, int width, int height, Action<int, float[]> fillRow)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Image size must be positive");

            var header = BuildHeader(width, height);
            var lineDataSize = width * 3 * 2;          // 3 channels * 2 bytes (HALF)
            var blockSize = 8 + lineDataSize;          // int32 y + int32 size + pixel data

            using (var w = new BinaryWriter(stream, Encoding.ASCII, true))
            {
                w.Write(header);

                // scanline offset table (one entry per row, uncompressed = one row per block)
                var firstBlock = (long)header.Length + 8L * height;
                for (var y = 0; y < height; y++)
                    w.Write((ulong)(firstBlock + (long)y * blockSize));

                var chunk = new byte[(long)RowsPerChunk * blockSize];
                for (var y0 = 0; y0 < height; y0 += RowsPerChunk)
                {
                    var rows = Math.Min(RowsPerChunk, height - y0);
                    var start = y0;
                    Parallel.For(0, rows, i =>
                    {
                        var y = start + i;
                        var rgb = new float[width * 3];
                        fillRow(y, rgb);

                        var span = new Span<byte>(chunk, i * blockSize, blockSize);
                        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(0, 4), y);
                        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(4, 4), lineDataSize);
                        // EXR stores channels alphabetically within each scanline: B, G, R
                        for (var x = 0; x < width; x++)
                        {
                            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(8 + x * 2, 2), ToHalfBits(rgb[x * 3 + 2]));
                            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(8 + (width + x) * 2, 2), ToHalfBits(rgb[x * 3 + 1]));
                            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(8 + (2 * width + x) * 2, 2), ToHalfBits(rgb[x * 3]));
                        }
                    });
                    w.Write(chunk, 0, rows * blockSize);
                }
            }
        }

        private static ushort ToHalfBits(float v)
        {
            if (float.IsNaN(v)) v = 0f;
            v = Math.Clamp(v, -65504f, 65504f);   // avoid +/-Inf for values beyond HALF range
            return BitConverter.HalfToUInt16Bits((System.Half)v);
        }

        private static byte[] BuildHeader(int width, int height)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.ASCII))
            {
                w.Write(new byte[] { 0x76, 0x2f, 0x31, 0x01 });   // magic
                w.Write(new byte[] { 0x02, 0x00, 0x00, 0x00 });   // version 2, no flags

                // channels: B, G, R (alphabetical), HALF, pLinear=0, xSampling=ySampling=1
                using (var ch = new MemoryStream())
                using (var cw = new BinaryWriter(ch, Encoding.ASCII))
                {
                    foreach (var name in new[] { "B", "G", "R" })
                    {
                        cw.Write(Encoding.ASCII.GetBytes(name));
                        cw.Write((byte)0);
                        cw.Write(1);                         // pixel type: HALF
                        cw.Write(new byte[] { 0, 0, 0, 0 }); // pLinear + reserved
                        cw.Write(1);                         // xSampling
                        cw.Write(1);                         // ySampling
                    }
                    cw.Write((byte)0);                       // end of channel list
                    WriteAttribute(w, "channels", "chlist", ch.ToArray());
                }

                WriteAttribute(w, "compression", "compression", new byte[] { 0 });   // NO_COMPRESSION

                var box = new byte[16];
                BinaryPrimitives.WriteInt32LittleEndian(box.AsSpan(0, 4), 0);
                BinaryPrimitives.WriteInt32LittleEndian(box.AsSpan(4, 4), 0);
                BinaryPrimitives.WriteInt32LittleEndian(box.AsSpan(8, 4), width - 1);
                BinaryPrimitives.WriteInt32LittleEndian(box.AsSpan(12, 4), height - 1);
                WriteAttribute(w, "dataWindow", "box2i", box);
                WriteAttribute(w, "displayWindow", "box2i", box);

                WriteAttribute(w, "lineOrder", "lineOrder", new byte[] { 0 });       // INCREASING_Y
                WriteAttribute(w, "pixelAspectRatio", "float", BitConverter.GetBytes(1f));
                WriteAttribute(w, "screenWindowCenter", "v2f", new byte[8]);
                WriteAttribute(w, "screenWindowWidth", "float", BitConverter.GetBytes(1f));

                w.Write((byte)0);                                                    // end of header
                return ms.ToArray();
            }
        }

        private static void WriteAttribute(BinaryWriter w, string name, string type, byte[] value)
        {
            w.Write(Encoding.ASCII.GetBytes(name)); w.Write((byte)0);
            w.Write(Encoding.ASCII.GetBytes(type)); w.Write((byte)0);
            w.Write(value.Length);
            w.Write(value);
        }
    }
}
