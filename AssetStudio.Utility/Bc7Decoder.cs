using System;

namespace AssetStudio
{
    /// <summary>
    /// BC7 (BPTC) decoder producing 8-bit RGBA.
    /// This is a C# port of bcdec_bc7() from bcdec.h by Sergii "iOrange" Kudlai
    /// (https://github.com/iOrange/bcdec, public domain / MIT-0). Layouts follow
    /// https://docs.microsoft.com/en-us/windows/win32/direct3d11/bc7-format
    /// </summary>
    internal static class Bc7Decoder
    {
        private static readonly int[][] ActualBitsCount = new int[][]
        {
            new[] { 4, 6, 5, 7, 5, 7, 7, 5 },   // RGB
            new[] { 0, 0, 0, 0, 6, 8, 7, 5 },   // Alpha
        };

        // 64 partition shapes for 2-subset BPTC, 4x4 each; fix-up indices have the MSB (128) set
        private static readonly byte[] PartitionSets2 = new byte[]
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
            128, 0, 129, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 1, 0, 0,
            128, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 129,
            128, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 129,
            128, 1, 0, 1, 1, 0, 129, 0, 0, 1, 0, 1, 1, 0, 1, 0,
            128, 0, 1, 1, 0, 0, 1, 1, 129, 1, 0, 0, 1, 1, 0, 0,
            128, 0, 129, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0,
            128, 1, 0, 1, 0, 1, 0, 1, 129, 0, 1, 0, 1, 0, 1, 0,
            128, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0, 0, 129,
            128, 1, 0, 1, 1, 0, 1, 0, 1, 0, 1, 0, 0, 1, 0, 129,
            128, 1, 129, 1, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 1, 0,
            128, 0, 0, 1, 0, 0, 1, 1, 129, 1, 0, 0, 1, 0, 0, 0,
            128, 0, 129, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 1, 0, 0,
            128, 0, 129, 1, 1, 0, 1, 1, 1, 1, 0, 1, 1, 1, 0, 0,
            128, 1, 129, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0,
            128, 0, 1, 1, 1, 1, 0, 0, 1, 1, 0, 0, 0, 0, 1, 129,
            128, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 129,
            128, 0, 0, 0, 0, 1, 129, 0, 0, 1, 1, 0, 0, 0, 0, 0,
            128, 1, 0, 0, 1, 1, 129, 0, 0, 1, 0, 0, 0, 0, 0, 0,
            128, 0, 129, 0, 0, 1, 1, 1, 0, 0, 1, 0, 0, 0, 0, 0,
            128, 0, 0, 0, 0, 0, 129, 0, 0, 1, 1, 1, 0, 0, 1, 0,
            128, 0, 0, 0, 0, 1, 0, 0, 129, 1, 1, 0, 0, 1, 0, 0,
            128, 1, 1, 0, 1, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 129,
            128, 0, 1, 1, 0, 1, 1, 0, 1, 1, 0, 0, 1, 0, 0, 129,
            128, 1, 129, 0, 0, 0, 1, 1, 1, 0, 0, 1, 1, 1, 0, 0,
            128, 0, 129, 1, 1, 0, 0, 1, 1, 1, 0, 0, 0, 1, 1, 0,
            128, 1, 1, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 0, 0, 129,
            128, 1, 1, 0, 0, 0, 1, 1, 0, 0, 1, 1, 1, 0, 0, 129,
            128, 1, 1, 1, 1, 1, 1, 0, 1, 0, 0, 0, 0, 0, 0, 129,
            128, 0, 0, 1, 1, 0, 0, 0, 1, 1, 1, 0, 0, 1, 1, 129,
            128, 0, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 0, 0, 1, 129,
            128, 0, 129, 1, 0, 0, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0,
            128, 0, 129, 0, 0, 0, 1, 0, 1, 1, 1, 0, 1, 1, 1, 0,
            128, 1, 0, 0, 0, 1, 0, 0, 0, 1, 1, 1, 0, 1, 1, 129
        };

        // 64 partition shapes for 3-subset BPTC
        private static readonly byte[] PartitionSets3 = new byte[]
        {
            128, 0, 1, 129, 0, 0, 1, 1, 0, 2, 2, 1, 2, 2, 2, 130,
            128, 0, 0, 129, 0, 0, 1, 1, 130, 2, 1, 1, 2, 2, 2, 1,
            128, 0, 0, 0, 2, 0, 0, 1, 130, 2, 1, 1, 2, 2, 1, 129,
            128, 2, 2, 130, 0, 0, 2, 2, 0, 0, 1, 1, 0, 1, 1, 129,
            128, 0, 0, 0, 0, 0, 0, 0, 129, 1, 2, 2, 1, 1, 2, 130,
            128, 0, 1, 129, 0, 0, 1, 1, 0, 0, 2, 2, 0, 0, 2, 130,
            128, 0, 2, 130, 0, 0, 2, 2, 1, 1, 1, 1, 1, 1, 1, 129,
            128, 0, 1, 1, 0, 0, 1, 1, 130, 2, 1, 1, 2, 2, 1, 129,
            128, 0, 0, 0, 0, 0, 0, 0, 129, 1, 1, 1, 2, 2, 2, 130,
            128, 0, 0, 0, 1, 1, 1, 1, 129, 1, 1, 1, 2, 2, 2, 130,
            128, 0, 0, 0, 1, 1, 129, 1, 2, 2, 2, 2, 2, 2, 2, 130,
            128, 0, 1, 2, 0, 0, 129, 2, 0, 0, 1, 2, 0, 0, 1, 130,
            128, 1, 1, 2, 0, 1, 129, 2, 0, 1, 1, 2, 0, 1, 1, 130,
            128, 1, 2, 2, 0, 129, 2, 2, 0, 1, 2, 2, 0, 1, 2, 130,
            128, 0, 1, 129, 0, 1, 1, 2, 1, 1, 2, 2, 1, 2, 2, 130,
            128, 0, 1, 129, 2, 0, 0, 1, 130, 2, 0, 0, 2, 2, 2, 0,
            128, 0, 0, 129, 0, 0, 1, 1, 0, 1, 1, 2, 1, 1, 2, 130,
            128, 1, 1, 129, 0, 0, 1, 1, 130, 0, 0, 1, 2, 2, 0, 0,
            128, 0, 0, 0, 1, 1, 2, 2, 129, 1, 2, 2, 1, 1, 2, 130,
            128, 0, 2, 130, 0, 0, 2, 2, 0, 0, 2, 2, 1, 1, 1, 129,
            128, 1, 1, 129, 0, 1, 1, 1, 0, 2, 2, 2, 0, 2, 2, 130,
            128, 0, 0, 129, 0, 0, 0, 1, 130, 2, 2, 1, 2, 2, 2, 1,
            128, 0, 0, 0, 0, 0, 129, 1, 0, 1, 2, 2, 0, 1, 2, 130,
            128, 0, 0, 0, 1, 1, 0, 0, 130, 2, 129, 0, 2, 2, 1, 0,
            128, 1, 2, 130, 0, 129, 2, 2, 0, 0, 1, 1, 0, 0, 0, 0,
            128, 0, 1, 2, 0, 0, 1, 2, 129, 1, 2, 2, 2, 2, 2, 130,
            128, 1, 1, 0, 1, 2, 130, 1, 129, 2, 2, 1, 0, 1, 1, 0,
            128, 0, 0, 0, 0, 1, 129, 0, 1, 2, 130, 1, 1, 2, 2, 1,
            128, 0, 2, 2, 1, 1, 0, 2, 129, 1, 0, 2, 0, 0, 2, 130,
            128, 1, 1, 0, 0, 129, 1, 0, 2, 0, 0, 2, 2, 2, 2, 130,
            128, 0, 1, 1, 0, 1, 2, 2, 0, 1, 130, 2, 0, 0, 1, 129,
            128, 0, 0, 0, 2, 0, 0, 0, 130, 2, 1, 1, 2, 2, 2, 129,
            128, 0, 0, 0, 0, 0, 0, 2, 129, 1, 2, 2, 1, 2, 2, 130,
            128, 2, 2, 130, 0, 0, 2, 2, 0, 0, 1, 2, 0, 0, 1, 129,
            128, 0, 1, 129, 0, 0, 1, 2, 0, 0, 2, 2, 0, 2, 2, 130,
            128, 1, 2, 0, 0, 129, 2, 0, 0, 1, 130, 0, 0, 1, 2, 0,
            128, 0, 0, 0, 1, 1, 129, 1, 2, 2, 130, 2, 0, 0, 0, 0,
            128, 1, 2, 0, 1, 2, 0, 1, 130, 0, 129, 2, 0, 1, 2, 0,
            128, 1, 2, 0, 2, 0, 1, 2, 129, 130, 0, 1, 0, 1, 2, 0,
            128, 0, 1, 1, 2, 2, 0, 0, 1, 1, 130, 2, 0, 0, 1, 129,
            128, 0, 1, 1, 1, 1, 130, 2, 2, 2, 0, 0, 0, 0, 1, 129,
            128, 1, 0, 129, 0, 1, 0, 1, 2, 2, 2, 2, 2, 2, 2, 130,
            128, 0, 0, 0, 0, 0, 0, 0, 130, 1, 2, 1, 2, 1, 2, 129,
            128, 0, 2, 2, 1, 129, 2, 2, 0, 0, 2, 2, 1, 1, 2, 130,
            128, 0, 2, 130, 0, 0, 1, 1, 0, 0, 2, 2, 0, 0, 1, 129,
            128, 2, 2, 0, 1, 2, 130, 1, 0, 2, 2, 0, 1, 2, 2, 129,
            128, 1, 0, 1, 2, 2, 130, 2, 2, 2, 2, 2, 0, 1, 0, 129,
            128, 0, 0, 0, 2, 1, 2, 1, 130, 1, 2, 1, 2, 1, 2, 129,
            128, 1, 0, 129, 0, 1, 0, 1, 0, 1, 0, 1, 2, 2, 2, 130,
            128, 2, 2, 130, 0, 1, 1, 1, 0, 2, 2, 2, 0, 1, 1, 129,
            128, 0, 0, 2, 1, 129, 1, 2, 0, 0, 0, 2, 1, 1, 1, 130,
            128, 0, 0, 0, 2, 129, 1, 2, 2, 1, 1, 2, 2, 1, 1, 130,
            128, 2, 2, 2, 0, 129, 1, 1, 0, 1, 1, 1, 0, 2, 2, 130,
            128, 0, 0, 2, 1, 1, 1, 2, 129, 1, 1, 2, 0, 0, 0, 130,
            128, 1, 1, 0, 0, 129, 1, 0, 0, 1, 1, 0, 2, 2, 2, 130,
            128, 0, 0, 0, 0, 0, 0, 0, 2, 1, 129, 2, 2, 1, 1, 130,
            128, 1, 1, 0, 0, 129, 1, 0, 2, 2, 2, 2, 2, 2, 2, 130,
            128, 0, 2, 2, 0, 0, 1, 1, 0, 0, 129, 1, 0, 0, 2, 130,
            128, 0, 2, 2, 1, 1, 2, 2, 129, 1, 2, 2, 0, 0, 2, 130,
            128, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 129, 1, 130,
            128, 0, 0, 130, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 129,
            128, 2, 2, 2, 1, 2, 2, 2, 0, 2, 2, 2, 129, 2, 2, 130,
            128, 1, 0, 129, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 130,
            128, 1, 1, 129, 2, 0, 1, 1, 130, 2, 0, 1, 2, 2, 2, 0
        };

        private static readonly int[] Weight2 = { 0, 21, 43, 64 };
        private static readonly int[] Weight3 = { 0, 9, 18, 27, 37, 46, 55, 64 };
        private static readonly int[] Weight4 = { 0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64 };

        private const int ModeHasPBits = 0b11001011;

        private struct BitStream
        {
            public ulong Low;
            public ulong High;

            public int Read(int numBits)
            {
                ulong mask = (1UL << numBits) - 1;
                int bits = (int)(Low & mask);
                Low >>= numBits;
                Low |= (High & mask) << (64 - numBits);
                High >>= numBits;
                return bits;
            }
        }

        private static int Interpolate(int a, int b, int[] weights, int index)
            => (a * (64 - weights[index]) + b * weights[index] + 32) >> 6;

        /// <summary>Decodes one 16-byte block into 16 RGBA pixels (row-major, 64 bytes).</summary>
        public static void DecodeBlock(byte[] data, int offset, byte[] output)
        {
            var bs = new BitStream
            {
                Low = BitConverter.ToUInt64(data, offset),
                High = BitConverter.ToUInt64(data, offset + 8),
            };

            int mode;
            for (mode = 0; mode < 8 && bs.Read(1) == 0; ++mode) { }

            // unexpected mode: transparent black
            if (mode >= 8)
            {
                Array.Clear(output, 0, 64);
                return;
            }

            int partition = 0, numPartitions = 1, rotation = 0, indexSelectionBit = 0;

            if (mode == 0 || mode == 1 || mode == 2 || mode == 3 || mode == 7)
            {
                numPartitions = (mode == 0 || mode == 2) ? 3 : 2;
                partition = bs.Read(mode == 0 ? 4 : 6);
            }

            int numEndpoints = numPartitions * 2;

            if (mode == 4 || mode == 5)
            {
                rotation = bs.Read(2);
                if (mode == 4)
                    indexSelectionBit = bs.Read(1);
            }

            var endpoints = new int[6, 4];
            int i, j, k;

            // RGB
            for (i = 0; i < 3; ++i)
                for (j = 0; j < numEndpoints; ++j)
                    endpoints[j, i] = bs.Read(ActualBitsCount[0][mode]);
            // alpha (if any)
            if (ActualBitsCount[1][mode] > 0)
                for (j = 0; j < numEndpoints; ++j)
                    endpoints[j, 3] = bs.Read(ActualBitsCount[1][mode]);

            // modes with P-bits
            if (mode == 0 || mode == 1 || mode == 3 || mode == 6 || mode == 7)
            {
                for (i = 0; i < numEndpoints; ++i)
                    for (j = 0; j < 4; ++j)
                        endpoints[i, j] <<= 1;

                if (mode == 1)
                {
                    // the P-bit is shared per subset
                    i = bs.Read(1);
                    j = bs.Read(1);
                    for (k = 0; k < 3; ++k)
                    {
                        endpoints[0, k] |= i;
                        endpoints[1, k] |= i;
                        endpoints[2, k] |= j;
                        endpoints[3, k] |= j;
                    }
                }
                else if ((ModeHasPBits & (1 << mode)) != 0)
                {
                    // unique P-bit per endpoint
                    for (i = 0; i < numEndpoints; ++i)
                    {
                        j = bs.Read(1);
                        for (k = 0; k < 4; ++k)
                            endpoints[i, k] |= j;
                    }
                }
            }

            for (i = 0; i < numEndpoints; ++i)
            {
                // colour precision including the P-bit
                j = ActualBitsCount[0][mode] + ((ModeHasPBits >> mode) & 1);
                for (k = 0; k < 3; ++k)
                {
                    // shift the MSB into bit 7, then replicate it into the revealed LSBs
                    endpoints[i, k] = endpoints[i, k] << (8 - j);
                    endpoints[i, k] = endpoints[i, k] | (endpoints[i, k] >> j);
                }

                j = ActualBitsCount[1][mode] + ((ModeHasPBits >> mode) & 1);
                endpoints[i, 3] = endpoints[i, 3] << (8 - j);
                endpoints[i, 3] = endpoints[i, 3] | (endpoints[i, 3] >> j);
            }

            // modes without alpha: alpha = 1.0
            if (ActualBitsCount[1][mode] == 0)
                for (j = 0; j < numEndpoints; ++j)
                    endpoints[j, 3] = 0xFF;

            int indexBits = (mode == 0 || mode == 1) ? 3 : ((mode == 6) ? 4 : 2);
            int indexBits2 = (mode == 4) ? 3 : ((mode == 5) ? 2 : 0);
            var weights = (indexBits == 2) ? Weight2 : ((indexBits == 3) ? Weight3 : Weight4);
            var weights2 = (indexBits2 == 2) ? Weight2 : Weight3;

            var partitionTable = numPartitions == 3 ? PartitionSets3 : PartitionSets2;
            var indices = new int[16];
            int partitionSet;

            // pass 1: colour indices
            for (i = 0; i < 4; ++i)
            {
                for (j = 0; j < 4; ++j)
                {
                    partitionSet = (numPartitions == 1) ? (((i | j) != 0) ? 0 : 128) : partitionTable[partition * 16 + i * 4 + j];

                    indexBits = (mode == 0 || mode == 1) ? 3 : ((mode == 6) ? 4 : 2);
                    // the fix-up index is stored with one bit less; the fix-up index of subset 0 is always index 0
                    if ((partitionSet & 0x80) != 0)
                        indexBits--;

                    indices[i * 4 + j] = bs.Read(indexBits);
                }
            }

            // pass 2: alpha indices (if any), interpolation and channel rotation
            int r, g, b, a, index, index2;
            for (i = 0; i < 4; ++i)
            {
                for (j = 0; j < 4; ++j)
                {
                    partitionSet = (numPartitions == 1) ? (((i | j) != 0) ? 0 : 128) : partitionTable[partition * 16 + i * 4 + j];
                    partitionSet &= 0x03;

                    index = indices[i * 4 + j];
                    int e0 = partitionSet * 2, e1 = partitionSet * 2 + 1;

                    if (indexBits2 == 0)
                    {
                        r = Interpolate(endpoints[e0, 0], endpoints[e1, 0], weights, index);
                        g = Interpolate(endpoints[e0, 1], endpoints[e1, 1], weights, index);
                        b = Interpolate(endpoints[e0, 2], endpoints[e1, 2], weights, index);
                        a = Interpolate(endpoints[e0, 3], endpoints[e1, 3], weights, index);
                    }
                    else
                    {
                        index2 = bs.Read(((i | j) != 0) ? indexBits2 : (indexBits2 - 1));
                        if (indexSelectionBit == 0)
                        {
                            r = Interpolate(endpoints[e0, 0], endpoints[e1, 0], weights, index);
                            g = Interpolate(endpoints[e0, 1], endpoints[e1, 1], weights, index);
                            b = Interpolate(endpoints[e0, 2], endpoints[e1, 2], weights, index);
                            a = Interpolate(endpoints[e0, 3], endpoints[e1, 3], weights2, index2);
                        }
                        else
                        {
                            r = Interpolate(endpoints[e0, 0], endpoints[e1, 0], weights2, index2);
                            g = Interpolate(endpoints[e0, 1], endpoints[e1, 1], weights2, index2);
                            b = Interpolate(endpoints[e0, 2], endpoints[e1, 2], weights2, index2);
                            a = Interpolate(endpoints[e0, 3], endpoints[e1, 3], weights, index);
                        }
                    }

                    int t;
                    switch (rotation)
                    {
                        case 1: t = a; a = r; r = t; break;   // scalar(R), vector(AGB): swap A and R
                        case 2: t = a; a = g; g = t; break;   // scalar(G), vector(RAB): swap A and G
                        case 3: t = a; a = b; b = t; break;   // scalar(B), vector(RGA): swap A and B
                    }

                    var o = (i * 4 + j) * 4;
                    output[o + 0] = (byte)r;
                    output[o + 1] = (byte)g;
                    output[o + 2] = (byte)b;
                    output[o + 3] = (byte)a;
                }
            }
        }

        /// <summary>
        /// Decodes a square BC7 image (size must be a multiple of 4) into 8-bit RGBA.
        /// The source starts at <paramref name="offset"/>; <paramref name="destination"/> needs size*size*4 bytes from <paramref name="destinationOffset"/>.
        /// </summary>
        public static void DecodeImage(byte[] source, int offset, int size, byte[] destination, int destinationOffset = 0)
        {
            var blocks = size / 4;
            var block = new byte[64];
            for (int by = 0; by < blocks; by++)
            {
                for (int bx = 0; bx < blocks; bx++)
                {
                    DecodeBlock(source, offset + (by * blocks + bx) * 16, block);
                    for (int y = 0; y < 4; y++)
                        Buffer.BlockCopy(block, y * 16, destination, destinationOffset + (((by * 4 + y) * size) + bx * 4) * 4, 16);
                }
            }
        }
    }
}
