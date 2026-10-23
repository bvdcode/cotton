// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    internal class Crc32Accumulator(uint checksum = 0)
    {
        private static readonly uint[] Table = BuildTable();
        private uint _state = ~checksum;

        public uint Value => ~_state;

        public void Append(ReadOnlySpan<byte> data)
        {
            uint crc = _state;
            foreach (byte value in data)
            {
                crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
            }

            _state = crc;
        }

        public static uint Combine(uint prefix, uint suffix, long suffixLength)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(suffixLength);
            Span<uint> shift = stackalloc uint[32];
            Span<uint> squared = stackalloc uint[32];
            for (int bit = 0; bit < shift.Length; bit++)
            {
                uint value = 1u << bit;
                shift[bit] = Table[value & 0xff] ^ (value >> 8);
            }
            while (suffixLength > 0)
            {
                if ((suffixLength & 1) != 0)
                {
                    prefix = Apply(shift, prefix);
                }
                suffixLength >>= 1;
                if (suffixLength > 0)
                {
                    for (int bit = 0; bit < shift.Length; bit++)
                    {
                        squared[bit] = Apply(shift, shift[bit]);
                    }
                    squared.CopyTo(shift);
                }
            }
            return prefix ^ suffix;
        }

        private static uint Apply(ReadOnlySpan<uint> matrix, uint value)
        {
            uint result = 0;
            for (int bit = 0; value != 0; bit++, value >>= 1)
            {
                if ((value & 1) != 0)
                {
                    result ^= matrix[bit];
                }
            }
            return result;
        }

        private static uint[] BuildTable()
        {
            uint[] table = new uint[256];
            for (uint i = 0; i < table.Length; i++)
            {
                uint crc = i;
                for (int bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 1) == 1
                        ? 0xEDB88320u ^ (crc >> 1)
                        : crc >> 1;
                }

                table[i] = crc;
            }

            return table;
        }
    }
}
