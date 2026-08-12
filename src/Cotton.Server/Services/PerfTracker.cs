// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Providers;

namespace Cotton.Server.Services
{
    public class PerfTracker(IServiceScopeFactory _scopeFactory)
    {
        private const int ChunkTimeoutSeconds = 10;
        private DateTime? _lastChunkCreated;
        private DateTime? _lastPreviewGenerating;

        public void OnChunkCreated()
        {
            _lastChunkCreated = DateTime.UtcNow;
        }

        public bool IsUploading()
        {
            if (_lastChunkCreated is null)
            {
                return false;
            }
            return (DateTime.UtcNow - _lastChunkCreated.Value).TotalSeconds < ChunkTimeoutSeconds;
        }

        public bool IsNightTime()
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            SettingsProvider settings = scope.ServiceProvider.GetRequiredService<SettingsProvider>();
            TimeZoneInfo tzInfo = settings.GetServerSettings().GetTimezoneInfo();
            DateTime localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tzInfo);
            return localTime.Hour < 7 || localTime.Hour >= 22;
        }

        public void OnPreviewGenerating()
        {
            _lastPreviewGenerating = DateTime.UtcNow;
        }

        public bool IsPreviewGenerating()
        {
            if (_lastPreviewGenerating is null)
            {
                return false;
            }
            return (DateTime.UtcNow - _lastPreviewGenerating.Value).TotalSeconds < ChunkTimeoutSeconds;
        }
    }
}
