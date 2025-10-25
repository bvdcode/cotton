﻿// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using System.Text;
using Cotton.Crypto;
using Cotton.Server.Settings;
using Cotton.Crypto.Abstractions;
using System.Security.Cryptography;

namespace Cotton.Server.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddStreamCipher(this IServiceCollection services)
        {
            return services.AddScoped<IStreamCipher>(sp =>
            {
                var settings = sp.GetRequiredService<CottonSettings>();
                if (string.IsNullOrWhiteSpace(settings.MasterEncryptionKey))
                {
                    throw new InvalidOperationException("MasterEncryptionKey is not configured.");
                }
                // Derive 32-byte key (SHA-256 of provided string)
                byte[] keyMaterial = SHA256.HashData(Encoding.UTF8.GetBytes(settings.MasterEncryptionKey));
                int keyId = settings.MasterEncryptionKeyId;
                int? threads = settings.EncryptionThreads.HasValue && settings.EncryptionThreads > 0 ? settings.EncryptionThreads : null;
                return new AesGcmStreamCipher(keyMaterial, keyId, threads);
            });
        }
    }
}
