// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using Cotton.Server.Services.KeyManagement;
using EasyExtensions.Abstractions;

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
            int? threads = ResolveThreads(encryptionThreadsOverride, settings.EncryptionThreads);

            return new AesGcmStreamCipher(keyMaterial, settings.MasterEncryptionKeyId, threads);
        }

        public static IStreamCipher Create(
            KeyringPlainState keyringState,
            CottonEncryptionSettings settings,
            int? encryptionThreadsOverride = null)
        {
            ArgumentNullException.ThrowIfNull(keyringState);
            ArgumentNullException.ThrowIfNull(settings);

            int? threads = ResolveThreads(encryptionThreadsOverride, settings.EncryptionThreads);
            var resolver = new KeyringPlainStateKeyResolver(keyringState);
            return new KeyringStreamCipher(resolver, KeyringKeyPurpose.ChunkAead, threads);
        }

        private static int? ResolveThreads(int? encryptionThreadsOverride, int configuredThreads)
        {
            int? threads = encryptionThreadsOverride.GetValueOrDefault() > 0
                ? encryptionThreadsOverride
                : configuredThreads > 0
                    ? configuredThreads
                    : null;
            if (threads.HasValue)
            {
                int maxThreads = Math.Max(1, Environment.ProcessorCount * 2);
                threads = Math.Clamp(threads.Value, 1, maxThreads);
            }

            return threads;
        }
    }
}
