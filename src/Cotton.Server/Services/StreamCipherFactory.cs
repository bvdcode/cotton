// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;

namespace Cotton.Server.Services
{
    internal static class StreamCipherFactory
    {
        public static AesGcmStreamCipher Create(
            CottonEncryptionSettings settings,
            int? encryptionThreadsOverride = null)
        {
            if (string.IsNullOrWhiteSpace(settings.MasterEncryptionKey))
            {
                throw new InvalidOperationException("MasterEncryptionKey is not configured.");
            }

            byte[] keyMaterial = Convert.FromBase64String(settings.MasterEncryptionKey);
            int? threads = encryptionThreadsOverride.GetValueOrDefault() > 0
                ? encryptionThreadsOverride
                : settings.EncryptionThreads > 0
                    ? settings.EncryptionThreads
                    : null;
            if (threads.HasValue)
            {
                int maxThreads = Math.Max(1, Environment.ProcessorCount * 2);
                threads = Math.Clamp(threads.Value, 1, maxThreads);
            }

            return new AesGcmStreamCipher(keyMaterial, settings.MasterEncryptionKeyId, threads);
        }
    }
}
