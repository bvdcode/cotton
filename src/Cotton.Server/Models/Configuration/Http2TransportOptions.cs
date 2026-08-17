// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Configuration
{
    public class Http2TransportOptions
    {
        private const int MinimumWindowSize = 65_535;

        public const string SectionName = "Http2Transport";

        public int InitialConnectionWindowSize { get; set; }

        public int InitialStreamWindowSize { get; set; }

        public void Validate()
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(
                InitialConnectionWindowSize,
                MinimumWindowSize);
            ArgumentOutOfRangeException.ThrowIfLessThan(
                InitialStreamWindowSize,
                MinimumWindowSize);
            ArgumentOutOfRangeException.ThrowIfLessThan(
                InitialConnectionWindowSize,
                InitialStreamWindowSize);
        }
    }
}
