// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models;

public readonly record struct FileVersionCaptureResult(bool Captured, long RemovedBytes)
{
    public static FileVersionCaptureResult Empty { get; } = new(Captured: false, RemovedBytes: 0);
}
