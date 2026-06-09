// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Logging;

namespace Cotton.Sdk.Tests.Fakes
{
    internal record RecordingLogEntry(
        string CategoryName,
        LogLevel Level,
        string Message,
        Exception? Exception);
}
