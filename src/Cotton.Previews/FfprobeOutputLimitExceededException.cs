// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews
{
    public class FfprobeOutputLimitExceededException(string outputName, int maxBytes)
        : Exception($"ffprobe {outputName} exceeded the configured {maxBytes}-byte limit.")
    {
        public string OutputName { get; } = outputName;

        public int MaxBytes { get; } = maxBytes;
    }
}
