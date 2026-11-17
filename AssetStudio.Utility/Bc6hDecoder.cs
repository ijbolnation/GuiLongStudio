using System;

namespace AssetStudio
{
    /// <summary>
    /// BC6H (HDR block compression) decoder producing half-float bit patterns / floats.
    /// This is a C# port of bcdec_bc6h_half() from bcdec.h by Sergii "iOrange" Kudlai
    /// (https://github.com/iOrange/bcdec, public domain / MIT-0). Layouts follow
    /// https://docs.microsoft.com/en-us/windows/win32/direct3d11/bc6h-format
    /// </summary>
    internal static class Bc6hDecoder
    {
        private static readonly int[][] ActualBitsCount = new int[][]
        {
            new[] { 10, 7, 11, 11, 11, 9, 8, 8, 8, 6, 10, 11, 12, 16 },   //  W
            new[] {  5, 6,  5,  4,  4, 5, 6, 5, 5, 6, 10,  9,  8,  4 },   // dR
            new[] {  5, 6,  4,  5,  4, 5, 5, 6, 5, 6, 10,  9,  8,  4 },   // dG
            new[] {  5, 6,  4,  4,  5, 5, 5, 5, 6, 6, 10,  9,  8,  4 },   // dB
        };

        // 32 two-region partition shapes, 4x4 each; the MSB (128) marks a fix-up index
        private static readonly byte[] PartitionSets = new byte[]
        {
        128, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 129,
        128, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 129,
        128, 1, 1, 1, 0, 1, 1, 1, 0, 1, 1, 1, 0, 1, 1, 129,
        128, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 1, 1, 129,
        128, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 1, 129,
        128, 0, 1, 1, 0, 1, 1, 1, 0, 1, 1, 1, 1, 1, 1, 129,
        128, 0, 0, 1, 0, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1, 129,
        128, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 1, 1, 129,
        128, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 129,
        128, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 129,
        128, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1, 1, 1, 1, 1, 129,
        128, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1, 129,
        128, 0, 0, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 129,
        128, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 129,
        128, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 129,
        128, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 129,
        128, 0, 0, 0, 1, 0, 0, 0, 1, 1, 1, 0, 1, 1, 1, 129,
        128, 1, 129, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0,
        128, 0, 0, 0, 0, 0, 0, 0, 129, 0, 0, 0, 1, 1, 1, 0,
        128, 1, 129, 1, 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0,
        128, 0, 129, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0,
        128, 0, 0, 0, 1, 0, 0, 0, 129, 1, 0, 0, 1, 1, 1, 0,
        128, 0, 0, 0, 0, 0, 0, 0, 129, 0, 0, 0, 1, 1, 0, 0,
        128, 1, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 0, 129,
        128, 0, 129, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0,
        128, 0, 0, 0, 1, 0, 0, 0, 129, 0, 0, 0, 1, 1, 0, 0,
        128, 1, 129, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0,
        128, 0, 129, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 1, 0, 0,
        128, 0, 0, 1, 0, 1, 1, 1, 129, 1, 1, 0, 1, 0, 0, 0,
        128, 0, 0, 0, 1, 1, 1, 1, 129, 1, 1, 1, 0, 0, 0, 0,
        128, 1, 129, 1, 0, 0, 0, 1, 1, 0, 0, 0, 1, 1, 1, 0,
        128, 0, 129, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 1, 0, 0
        };

        private static readonly int[] Weight3 = { 0, 9, 18, 27, 37, 46, 55, 64 };
        private static readonly int[] Weight4 = { 0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64 };

        private struct BitStream
        {
            public ulong Low;
            public ulong High;

            public int Read(int numBits)
            {
                ulong mask = (1UL << numBits) - 1;
                int bits = (int)(Low & mask);
                Low >>= numBits;
                // put the low N bits of High into the top N bits of Low
                Low |= (High & mask) << (64 - numBits);
                High >>= numBits;
                return bits;
            }

            // reversed bit order, used for the extended endpoint bits of modes 10 and above
            public int ReadReversed(int numBits)
            {
                int bits = Read(numBits);
                int result = 0;
                while (numBits-- > 0)
                {
                    result <<= 1;
                    result |= bits & 1;
                    bits >>= 1;
                }
                return result;
            }
        }

        private static int ExtendSign(int val, int bits) => (val << (32 - bits)) >> (32 - bits);

        private static int TransformInverse(int val, int a0, int bits, bool isSigned)
        {
            val = (val + a0) & ((1 << bits) - 1);
            if (isSigned)
                val = ExtendSign(val, bits);
            return val;
        }

        private static int Unquantize(int val, int bits, bool isSigned)
        {
            int unq, s = 0;
            if (!isSigned)
            {
                if (bits >= 15) unq = val;
                else if (val == 0) unq = 0;
                else if (val == ((1 << bits) - 1)) unq = 0xFFFF;
                else unq = ((val << 16) + 0x8000) >> bits;
            }
            else
            {
                if (bits >= 16) unq = val;
                else
                {
                    if (val < 0) { s = 1; val = -val; }
                    if (val == 0) unq = 0;
                    else if (val >= ((1 << (bits - 1)) - 1)) unq = 0x7FFF;
                    else unq = ((val << 15) + 0x4000) >> (bits - 1);
                    if (s != 0) unq = -unq;
                }
            }
            return unq;
        }

        private static int Interpolate(int a, int b, int[] weights, int index)
            => (a * (64 - weights[index]) + b * weights[index] + 32) >> 6;

        private static ushort FinishUnquantize(int val, bool isSigned)
        {
            if (!isSigned)
                return (ushort)((val * 31) >> 6);   // scale the magnitude by 31 / 64
            val = (val < 0) ? -(((-val) * 31) >> 5) : (val * 31) >> 5;   // scale the magnitude by 31 / 32
            int s = 0;
            if (val < 0) { s = 0x8000; val = -val; }
            return (ushort)(s | val);
        }

        /// <summary>Decodes one 16-byte block into 16 pixels x 3 channels of half-float bits (row-major, RGB).</summary>
        public static void DecodeBlockHalf(byte[] data, int offset, ushort[] output, bool isSigned = false)
        {
            var bs = new BitStream
            {
                Low = BitConverter.ToUInt64(data, offset),
                High = BitConverter.ToUInt64(data, offset + 8),
            };

            int mode, partition, numPartitions, actualBits0Mode;
            var r = new int[4];
            var g = new int[4];
            var b = new int[4];   // wxyz

            mode = bs.Read(2);
            if (mode > 1)
                mode |= bs.Read(3) << 2;

            // modes >= 11 (10 in this code) use partition 0; the others read it from the bitstream
            partition = 0;

    switch (mode) {
        /* mode 1 */
        case 0b00: {
            /* Partitition indices: 46 bits
               Partition: 5 bits
               Color Endpoints: 75 bits (10.555, 10.555, 10.555) */
            g[2] |= bs.Read(1) << 4;       /* gy[4]   */
            b[2] |= bs.Read(1) << 4;       /* by[4]   */
            b[3] |= bs.Read(1) << 4;       /* bz[4]   */
            r[0] |= bs.Read(10);       /* rw[9:0] */
            g[0] |= bs.Read(10);       /* gw[9:0] */
            b[0] |= bs.Read(10);       /* bw[9:0] */
            r[1] |= bs.Read(5);        /* rx[4:0] */
            g[3] |= bs.Read(1) << 4;       /* gz[4]   */
            g[2] |= bs.Read(4);        /* gy[3:0] */
            g[1] |= bs.Read(5);        /* gx[4:0] */
            b[3] |= bs.Read(1);            /* bz[0]   */
            g[3] |= bs.Read(4);        /* gz[3:0] */
            b[1] |= bs.Read(5);        /* bx[4:0] */
            b[3] |= bs.Read(1) << 1;       /* bz[1]   */
            b[2] |= bs.Read(4);        /* by[3:0] */
            r[2] |= bs.Read(5);        /* ry[4:0] */
            b[3] |= bs.Read(1) << 2;       /* bz[2]   */
            r[3] |= bs.Read(5);        /* rz[4:0] */
            b[3] |= bs.Read(1) << 3;       /* bz[3]   */
            partition = bs.Read(5);    /* d[4:0]  */
            mode = 0;
        } break;

        /* mode 2 */
        case 0b01: {
            /* Partitition indices: 46 bits
               Partition: 5 bits
               Color Endpoints: 75 bits (7666, 7666, 7666) */
            g[2] |= bs.Read(1) << 5;       /* gy[5]   */
            g[3] |= bs.Read(1) << 4;       /* gz[4]   */
            g[3] |= bs.Read(1) << 5;       /* gz[5]   */
            r[0] |= bs.Read(7);        /* rw[6:0] */
            b[3] |= bs.Read(1);            /* bz[0]   */
            b[3] |= bs.Read(1) << 1;       /* bz[1]   */
            b[2] |= bs.Read(1) << 4;       /* by[4]   */
            g[0] |= bs.Read(7);        /* gw[6:0] */
            b[2] |= bs.Read(1) << 5;       /* by[5]   */
            b[3] |= bs.Read(1) << 2;       /* bz[2]   */
            g[2] |= bs.Read(1) << 4;       /* gy[4]   */
            b[0] |= bs.Read(7);        /* bw[6:0] */
            b[3] |= bs.Read(1) << 3;       /* bz[3]   */
            b[3] |= bs.Read(1) << 5;       /* bz[5]   */
            b[3] |= bs.Read(1) << 4;       /* bz[4]   */
            r[1] |= bs.Read(6);        /* rx[5:0] */
            g[2] |= bs.Read(4);        /* gy[3:0] */
            g[1] |= bs.Read(6);        /* gx[5:0] */
            g[3] |= bs.Read(4);        /* gz[3:0] */
            b[1] |= bs.Read(6);        /* bx[5:0] */
            b[2] |= bs.Read(4);        /* by[3:0] */
            r[2] |= bs.Read(6);        /* ry[5:0] */
            r[3] |= bs.Read(6);        /* rz[5:0] */
            partition = bs.Read(5);    /* d[4:0]  */
            mode = 1;
        } break;

        /* mode 3 */
        case 0b00010: {
            /* Partitition indices: 46 bits
               Partition: 5 bits
               Color Endpoints: 72 bits (11.555, 11.444, 11.444) */
            r[0] |= bs.Read(10);       /* rw[9:0] */
            g[0] |= bs.Read(10);       /* gw[9:0] */
            b[0] |= bs.Read(10);       /* bw[9:0] */
            r[1] |= bs.Read(5);        /* rx[4:0] */
            r[0] |= bs.Read(1) << 10;      /* rw[10]  */
            g[2] |= bs.Read(4);        /* gy[3:0] */
            g[1] |= bs.Read(4);        /* gx[3:0] */
            g[0] |= bs.Read(1) << 10;      /* gw[10]  */
            b[3] |= bs.Read(1);            /* bz[0]   */
            g[3] |= bs.Read(4);        /* gz[3:0] */
            b[1] |= bs.Read(4);        /* bx[3:0] */
            b[0] |= bs.Read(1) << 10;      /* bw[10]  */
            b[3] |= bs.Read(1) << 1;       /* bz[1]   */
            b[2] |= bs.Read(4);        /* by[3:0] */
            r[2] |= bs.Read(5);        /* ry[4:0] */
            b[3] |= bs.Read(1) << 2;       /* bz[2]   */
            r[3] |= bs.Read(5);        /* rz[4:0] */
            b[3] |= bs.Read(1) << 3;       /* bz[3]   */
            partition = bs.Read(5);    /* d[4:0]  */
            mode = 2;
        } break;

        /* mode 4 */
        case 0b00110: {
            /* Partitition indices: 46 bits
               Partition: 5 bits
               Color Endpoints: 72 bits (11.444, 11.555, 11.444) */
            r[0] |= bs.Read(10);       /* rw[9:0] */
            g[0] |= bs.Read(10);       /* gw[9:0] */
            b[0] |= bs.Read(10);       /* bw[9:0] */
            r[1] |= bs.Read(4);        /* rx[3:0] */
            r[0] |= bs.Read(1) << 10;      /* rw[10]  */
            g[3] |= bs.Read(1) << 4;       /* gz[4]   */
            g[2] |= bs.Read(4);        /* gy[3:0] */
            g[1] |= bs.Read(5);        /* gx[4:0] */
            g[0] |= bs.Read(1) << 10;      /* gw[10]  */
            g[3] |= bs.Read(4);        /* gz[3:0] */
            b[1] |= bs.Read(4);        /* bx[3:0] */
            b[0] |= bs.Read(1) << 10;      /* bw[10]  */
            b[3] |= bs.Read(1) << 1;       /* bz[1]   */
            b[2] |= bs.Read(4);        /* by[3:0] */
            r[2] |= bs.Read(4);        /* ry[3:0] */
            b[3] |= bs.Read(1);            /* bz[0]   */
            b[3] |= bs.Read(1) << 2;       /* bz[2]   */
            r[3] |= bs.Read(4);        /* rz[3:0] */
            g[2] |= bs.Read(1) << 4;       /* gy[4]   */
            b[3] |= bs.Read(1) << 3;       /* bz[3]   */
            partition = bs.Read(5);    /* d[4:0]  */
            mode = 3;
        } break;

        /* mode 5 */
        case 0b01010: {
            /* Partitition indices: 46 bits
               Partition: 5 bits
               Color Endpoints: 72 bits (11.444, 11.444, 11.555) */
            r[0] |= bs.Read(10);       /* rw[9:0] */
            g[0] |= bs.Read(10);       /* gw[9:0] */
            b[0] |= bs.Read(10);       /* bw[9:0] */
            r[1] |= bs.Read(4);        /* rx[3:0] */
            r[0] |= bs.Read(1) << 10;      /* rw[10]  */
            b[2] |= bs.Read(1) << 4;       /* by[4]   */
            g[2] |= bs.Read(4);        /* gy[3:0] */
            g[1] |= bs.Read(4);        /* gx[3:0] */
            g[0] |= bs.Read(1) << 10;      /* gw[10]  */
            b[3] |= bs.Read(1);            /* bz[0]   */
            g[3] |= bs.Read(4);        /* gz[3:0] */
            b[1] |= bs.Read(5);        /* bx[4:0] */
            b[0] |= bs.Read(1) << 10;      /* bw[10]  */
            b[2] |= bs.Read(4);        /* by[3:0] */
            r[2] |= bs.Read(4);        /* ry[3:0] */
            b[3] |= bs.Read(1) << 1;       /* bz[1]   */
            b[3] |= bs.Read(1) << 2;       /* bz[2]   */
            r[3] |= bs.Read(4);        /* rz[3:0] */
            b[3] |= bs.Read(1) << 4;       /* bz[4]   */ 
            b[3] |= bs.Read(1) << 3;       /* bz[3]   */
            partition = bs.Read(5);    /* d[4:0]  */
            mode = 4;
        } break;

        /* mode 6 */
        case 0b01110: {
            /* Partitition indices: 46 bits
               Partition: 5 bits
               Color Endpoints: 72 bits (9555, 9555, 9555) */
            r[0] |= bs.Read(9);        /* rw[8:0] */
            b[2] |= bs.Read(1) << 4;       /* by[4]   */
            g[0] |= bs.Read(9);        /* gw[8:0] */
            g[2] |= bs.Read(1) << 4;       /* gy[4]   */
            b[0] |= bs.Read(9);        /* bw[8:0] */
            b[3] |= bs.Read(1) << 4;       /* bz[4]   */
            r[1] |= bs.Read(5);        /* rx[4:0] */
            g[3] |= bs.Read(1) << 4;       /* gz[4]   */
            g[2] |= bs.Read(4);        /* gy[3:0] */
            g[1] |= bs.Read(5);        /* gx[4:0] */
            b[3] |= bs.Read(1);            /* bz[0]   */
            g[3] |= bs.Read(4);        /* gx[3:0] */
            b[1] |= bs.Read(5);        /* bx[4:0] */
            b[3] |= bs.Read(1) << 1;       /* bz[1]   */
            b[2] |= bs.Read(4);        /* by[3:0] */
            r[2] |= bs.Read(5);        /* ry[4:0] */
            b[3] |= bs.Read(1) << 2;       /* bz[2]   */
            r[3] |= bs.Read(5);        /* rz[4:0] */
            b[3] |= bs.Read(1) << 3;       /* bz[3]   */
            partition = bs.Read(5);    /* d[4:0]  */
            mode = 5;
        } break;

        /* mode 7 */
        case 0b10010: {
            /* Partitition indices: 46 bits
               Partition: 5 bits
               Color Endpoints: 72 bits (8666, 8555, 8555) */
            r[0] |= bs.Read(8);        /* rw[7:0] */
            g[3] |= bs.Read(1) << 4;       /* gz[4]   */
            b[2] |= bs.Read(1) << 4;       /* by[4]   */
            g[0] |= bs.Read(8);        /* gw[7:0] */
            b[3] |= bs.Read(1) << 2;       /* bz[2]   */
            g[2] |= bs.Read(1) << 4;       /* gy[4]   */
            b[0] |= bs.Read(8);        /* bw[7:0] */
            b[3] |= bs.Read(1) << 3;       /* bz[3]   */
            b[3] |= bs.Read(1) << 4;       /* bz[4]   */
            r[1] |= bs.Read(6);        /* rx[5:0] */
            g[2] |= bs.Read(4);        /* gy[3:0] */
            g[1] |= bs.Read(5);        /* gx[4:0] */
            b[3] |= bs.Read(1);            /* bz[0]   */
            g[3] |= bs.Read(4);        /* gz[3:0] */
            b[1] |= bs.Read(5);        /* bx[4:0] */
            b[3] |= bs.Read(1) << 1;       /* bz[1]   */
            b[2] |= bs.Read(4);        /* by[3:0] */
            r[2] |= bs.Read(6);        /* ry[5:0] */
            r[3] |= bs.Read(6);        /* rz[5:0] */
            partition = bs.Read(5);    /* d[4:0]  */
            mode = 6;
        } break;

        /* mode 8 */
        case 0b10110: {
            /* Partitition indices: 46 bits
               Partition: 5 bits
               Color Endpoints: 72 bits (8555, 8666, 8555) */
            r[0] |= bs.Read(8);        /* rw[7:0] */
            b[3] |= bs.Read(1);            /* bz[0]   */
            b[2] |= bs.Read(1) << 4;       /* by[4]   */
            g[0] |= bs.Read(8);        /* gw[7:0] */
            g[2] |= bs.Read(1) << 5;       /* gy[5]   */
            g[2] |= bs.Read(1) << 4;       /* gy[4]   */
            b[0] |= bs.Read(8);        /* bw[7:0] */
            g[3] |= bs.Read(1) << 5;       /* gz[5]   */
            b[3] |= bs.Read(1) << 4;       /* bz[4]   */
            r[1] |= bs.Read(5);        /* rx[4:0] */
            g[3] |= bs.Read(1) << 4;       /* gz[4]   */
            g[2] |= bs.Read(4);        /* gy[3:0] */
            g[1] |= bs.Read(6);        /* gx[5:0] */
            g[3] |= bs.Read(4);        /* zx[3:0] */
            b[1] |= bs.Read(5);        /* bx[4:0] */
            b[3] |= bs.Read(1) << 1;       /* bz[1]   */
            b[2] |= bs.Read(4);        /* by[3:0] */
            r[2] |= bs.Read(5);        /* ry[4:0] */
            b[3] |= bs.Read(1) << 2;       /* bz[2]   */
            r[3] |= bs.Read(5);        /* rz[4:0] */
            b[3] |= bs.Read(1) << 3;       /* bz[3]   */
            partition = bs.Read(5);    /* d[4:0]  */
            mode = 7;
        } break;

        /* mode 9 */
        case 0b11010: {
            /* Partitition indices: 46 bits
               Partition: 5 bits
               Color Endpoints: 72 bits (8555, 8555, 8666) */
            r[0] |= bs.Read(8);        /* rw[7:0] */
            b[3] |= bs.Read(1) << 1;       /* bz[1]   */
            b[2] |= bs.Read(1) << 4;       /* by[4]   */
            g[0] |= bs.Read(8);        /* gw[7:0] */
            b[2] |= bs.Read(1) << 5;       /* by[5]   */
            g[2] |= bs.Read(1) << 4;       /* gy[4]   */
            b[0] |= bs.Read(8);        /* bw[7:0] */
            b[3] |= bs.Read(1) << 5;       /* bz[5]   */
            b[3] |= bs.Read(1) << 4;       /* bz[4]   */
            r[1] |= bs.Read(5);        /* bw[4:0] */
            g[3] |= bs.Read(1) << 4;       /* gz[4]   */
            g[2] |= bs.Read(4);        /* gy[3:0] */
            g[1] |= bs.Read(5);        /* gx[4:0] */
            b[3] |= bs.Read(1);            /* bz[0]   */
            g[3] |= bs.Read(4);        /* gz[3:0] */
            b[1] |= bs.Read(6);        /* bx[5:0] */
            b[2] |= bs.Read(4);        /* by[3:0] */
            r[2] |= bs.Read(5);        /* ry[4:0] */
            b[3] |= bs.Read(1) << 2;       /* bz[2]   */
            r[3] |= bs.Read(5);        /* rz[4:0] */
            b[3] |= bs.Read(1) << 3;       /* bz[3]   */
            partition = bs.Read(5);    /* d[4:0]  */
            mode = 8;
        } break;

        /* mode 10 */
        case 0b11110: {
            /* Partitition indices: 46 bits
               Partition: 5 bits
               Color Endpoints: 72 bits (6666, 6666, 6666) */
            r[0] |= bs.Read(6);        /* rw[5:0] */
            g[3] |= bs.Read(1) << 4;       /* gz[4]   */
            b[3] |= bs.Read(1);            /* bz[0]   */
            b[3] |= bs.Read(1) << 1;       /* bz[1]   */
            b[2] |= bs.Read(1) << 4;       /* by[4]   */
            g[0] |= bs.Read(6);        /* gw[5:0] */
            g[2] |= bs.Read(1) << 5;       /* gy[5]   */
            b[2] |= bs.Read(1) << 5;       /* by[5]   */
            b[3] |= bs.Read(1) << 2;       /* bz[2]   */
            g[2] |= bs.Read(1) << 4;       /* gy[4]   */
            b[0] |= bs.Read(6);        /* bw[5:0] */
            g[3] |= bs.Read(1) << 5;       /* gz[5]   */
            b[3] |= bs.Read(1) << 3;       /* bz[3]   */
            b[3] |= bs.Read(1) << 5;       /* bz[5]   */
            b[3] |= bs.Read(1) << 4;       /* bz[4]   */
            r[1] |= bs.Read(6);        /* rx[5:0] */
            g[2] |= bs.Read(4);        /* gy[3:0] */
            g[1] |= bs.Read(6);        /* gx[5:0] */
            g[3] |= bs.Read(4);        /* gz[3:0] */
            b[1] |= bs.Read(6);        /* bx[5:0] */
            b[2] |= bs.Read(4);        /* by[3:0] */
            r[2] |= bs.Read(6);        /* ry[5:0] */
            r[3] |= bs.Read(6);        /* rz[5:0] */
            partition = bs.Read(5);    /* d[4:0]  */
            mode = 9;
        } break;

        /* mode 11 */
        case 0b00011: {
            /* Partitition indices: 63 bits
               Partition: 0 bits
               Color Endpoints: 60 bits (10.10, 10.10, 10.10) */
            r[0] |= bs.Read(10);       /* rw[9:0] */
            g[0] |= bs.Read(10);       /* gw[9:0] */
            b[0] |= bs.Read(10);       /* bw[9:0] */
            r[1] |= bs.Read(10);       /* rx[9:0] */
            g[1] |= bs.Read(10);       /* gx[9:0] */
            b[1] |= bs.Read(10);       /* bx[9:0] */
            mode = 10;
        } break;

        /* mode 12 */
        case 0b00111: {
            /* Partitition indices: 63 bits
               Partition: 0 bits
               Color Endpoints: 60 bits (11.9, 11.9, 11.9) */
            r[0] |= bs.Read(10);       /* rw[9:0] */
            g[0] |= bs.Read(10);       /* gw[9:0] */
            b[0] |= bs.Read(10);       /* bw[9:0] */
            r[1] |= bs.Read(9);        /* rx[8:0] */
            r[0] |= bs.Read(1) << 10;      /* rw[10]  */
            g[1] |= bs.Read(9);        /* gx[8:0] */
            g[0] |= bs.Read(1) << 10;      /* gw[10]  */
            b[1] |= bs.Read(9);        /* bx[8:0] */
            b[0] |= bs.Read(1) << 10;      /* bw[10]  */
            mode = 11;
        } break;

        /* mode 13 */
        case 0b01011: {
            /* Partitition indices: 63 bits
               Partition: 0 bits
               Color Endpoints: 60 bits (12.8, 12.8, 12.8) */
            r[0] |= bs.Read(10);       /* rw[9:0] */
            g[0] |= bs.Read(10);       /* gw[9:0] */
            b[0] |= bs.Read(10);       /* bw[9:0] */
            r[1] |= bs.Read(8);        /* rx[7:0] */
            r[0] |= bs.ReadReversed(2) << 10;/* rx[10:11] */
            g[1] |= bs.Read(8);        /* gx[7:0] */
            g[0] |= bs.ReadReversed(2) << 10;/* gx[10:11] */
            b[1] |= bs.Read(8);        /* bx[7:0] */
            b[0] |= bs.ReadReversed(2) << 10;/* bx[10:11] */
            mode = 12;
        } break;

        /* mode 14 */
        case 0b01111: {
            /* Partitition indices: 63 bits
               Partition: 0 bits
               Color Endpoints: 60 bits (16.4, 16.4, 16.4) */
            r[0] |= bs.Read(10);       /* rw[9:0] */
            g[0] |= bs.Read(10);       /* gw[9:0] */
            b[0] |= bs.Read(10);       /* bw[9:0] */
            r[1] |= bs.Read(4);        /* rx[3:0] */
            r[0] |= bs.ReadReversed(6) << 10;/* rw[10:15] */
            g[1] |= bs.Read(4);        /* gx[3:0] */
            g[0] |= bs.ReadReversed(6) << 10;/* gw[10:15] */
            b[1] |= bs.Read(4);        /* bx[3:0] */
            b[0] |= bs.ReadReversed(6) << 10;/* bw[10:15] */
            mode = 13;
        } break;

        default:
        {
            // Reserved modes decode to all zeros
            Array.Clear(output, 0, 48);
            return;
        }
    }


            numPartitions = (mode >= 10) ? 0 : 1;
            actualBits0Mode = ActualBitsCount[0][mode];
            if (isSigned)
            {
                r[0] = ExtendSign(r[0], actualBits0Mode);
                g[0] = ExtendSign(g[0], actualBits0Mode);
                b[0] = ExtendSign(b[0], actualBits0Mode);
            }

            // Mode 11 (like Mode 10) does not use delta compression, and stores both endpoints explicitly
            if ((mode != 9 && mode != 10) || isSigned)
            {
                for (int i = 1; i < (numPartitions + 1) * 2; ++i)
                {
                    r[i] = ExtendSign(r[i], ActualBitsCount[1][mode]);
                    g[i] = ExtendSign(g[i], ActualBitsCount[2][mode]);
                    b[i] = ExtendSign(b[i], ActualBitsCount[3][mode]);
                }
            }

            if (mode != 9 && mode != 10)
            {
                for (int i = 1; i < (numPartitions + 1) * 2; ++i)
                {
                    r[i] = TransformInverse(r[i], r[0], actualBits0Mode, isSigned);
                    g[i] = TransformInverse(g[i], g[0], actualBits0Mode, isSigned);
                    b[i] = TransformInverse(b[i], b[0], actualBits0Mode, isSigned);
                }
            }

            for (int i = 0; i < (numPartitions + 1) * 2; ++i)
            {
                r[i] = Unquantize(r[i], actualBits0Mode, isSigned);
                g[i] = Unquantize(g[i], actualBits0Mode, isSigned);
                b[i] = Unquantize(b[i], actualBits0Mode, isSigned);
            }

            var weights = (mode >= 10) ? Weight4 : Weight3;
            for (int i = 0; i < 4; ++i)
            {
                for (int j = 0; j < 4; ++j)
                {
                    int partitionSet = (mode >= 10) ? ((i | j) != 0 ? 0 : 128) : PartitionSets[partition * 16 + i * 4 + j];
                    int indexBits = (mode >= 10) ? 4 : 3;
                    // the fix-up index is stored with one bit less; the fix-up index of subset 0 is always index 0
                    if ((partitionSet & 0x80) != 0)
                        indexBits--;
                    partitionSet &= 0x01;

                    int index = bs.Read(indexBits);
                    int epi = partitionSet * 2;
                    int o = (i * 4 + j) * 3;
                    output[o + 0] = FinishUnquantize(Interpolate(r[epi], r[epi + 1], weights, index), isSigned);
                    output[o + 1] = FinishUnquantize(Interpolate(g[epi], g[epi + 1], weights, index), isSigned);
                    output[o + 2] = FinishUnquantize(Interpolate(b[epi], b[epi + 1], weights, index), isSigned);
                }
            }
        }

        /// <summary>
        /// Decodes a square BC6H image (size must be a multiple of 4) into interleaved RGB float32.
        /// The source starts at <paramref name="offset"/>; <paramref name="destination"/> needs size*size*3 floats.
        /// </summary>
        public static void DecodeImage(byte[] source, int offset, int size, float[] destination, bool isSigned = false)
        {
            var blocks = size / 4;
            var half = new ushort[48];
            for (int by = 0; by < blocks; by++)
            {
                for (int bx = 0; bx < blocks; bx++)
                {
                    DecodeBlockHalf(source, offset + (by * blocks + bx) * 16, half, isSigned);
                    for (int y = 0; y < 4; y++)
                    {
                        for (int x = 0; x < 4; x++)
                        {
                            var dst = (((by * 4 + y) * size) + bx * 4 + x) * 3;
                            var src = (y * 4 + x) * 3;
                            destination[dst] = (float)BitConverter.UInt16BitsToHalf(half[src]);
                            destination[dst + 1] = (float)BitConverter.UInt16BitsToHalf(half[src + 1]);
                            destination[dst + 2] = (float)BitConverter.UInt16BitsToHalf(half[src + 2]);
                        }
                    }
                }
            }
        }
    }
}
