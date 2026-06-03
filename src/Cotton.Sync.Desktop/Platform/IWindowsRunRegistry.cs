// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Platform;

internal interface IWindowsRunRegistry
{
    string? GetValue(string valueName);

    void SetValue(string valueName, string value);

    void DeleteValue(string valueName);
}
