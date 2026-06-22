// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Providers;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Represents perf tracker.
    /// </summary>
    public class PerfTracker(IServiceScopeFactory _scopeFactory)
    {
        private const int ChunkTimeoutSeconds = 10;
        private DateTime? _lastChunkCreated;
        private DateTime? _lastPreviewGenerating;

        /// <summary>
        /// Records chunk creation metrics.
        /// </summary>
        public void OnChunkCreated()
        {
            _lastChunkCreated = DateTime.UtcNow;
        }

        /// <summary>
        /// Indicates whether uploading.
        /// </summary>
        public bool IsUploading()
        {
            if (_lastChunkCreated is null)
            {
                return false;
            }
            return (DateTime.UtcNow - _lastChunkCreated.Value).TotalSeconds < ChunkTimeoutSeconds;
        }

        /// <summary>
        /// Indicates whether night time.
        /// </summary>
        public bool IsNightTime()
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            SettingsProvider settings = scope.ServiceProvider.GetRequiredService<SettingsProvider>();
            TimeZoneInfo tzInfo = settings.GetServerSettings().GetTimezoneInfo();
            DateTime localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tzInfo);
            return localTime.Hour < 7 || localTime.Hour >= 22;
        }

        /// <summary>
        /// Records preview generation metrics.
        /// </summary>
        public void OnPreviewGenerating()
        {
            _lastPreviewGenerating = DateTime.UtcNow;
        }

        /// <summary>
        /// Indicates whether preview generating.
        /// </summary>
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
