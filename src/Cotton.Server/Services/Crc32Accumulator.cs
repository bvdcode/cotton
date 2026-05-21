// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services;

internal sealed class Crc32Accumulator
{
    private static readonly uint[] Table = BuildTable();
    private uint _state = 0xFFFFFFFFu;

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

    private static uint[] BuildTable()
    {
        var table = new uint[256];
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
