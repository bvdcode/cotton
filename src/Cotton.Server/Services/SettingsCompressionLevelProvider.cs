// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Providers;
using Cotton.Storage.Processors;

namespace Cotton.Server.Services
{
    public class SettingsCompressionLevelProvider(SettingsProvider settings) : ICompressionLevelProvider
    {
        public int Level => settings.GetServerSettings().CompressionLevel;
    }
}
