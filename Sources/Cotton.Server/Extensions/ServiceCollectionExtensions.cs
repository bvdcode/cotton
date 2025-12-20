// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Services;
using Cotton.Shared;
using EasyExtensions.Abstractions;
using EasyExtensions.Crypto;
using System.Text;

namespace Cotton.Server.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddStreamCipher(this IServiceCollection services)
        {
            return services.AddScoped<IStreamCipher>(sp =>
            {
                var settings = sp.GetRequiredService<CottonEncryptionSettings>();
                if (string.IsNullOrWhiteSpace(settings.MasterEncryptionKey))
                {
                    throw new InvalidOperationException("MasterEncryptionKey is not configured.");
                }
                // Derive 32-byte key (SHA-256 of provided string)
                byte[] keyMaterial = Hasher.HashData(Encoding.UTF8.GetBytes(settings.MasterEncryptionKey));
                int keyId = settings.MasterEncryptionKeyId;
                CottonSettingsService settingsService = sp.GetRequiredService<CottonSettingsService>();
                CottonServerSettings serverSettings = settingsService.GetServerSettings();
                int? threads = serverSettings.EncryptionThreads > 0 ? serverSettings.EncryptionThreads : null;
                return new AesGcmStreamCipher(keyMaterial, keyId, threads);
            });
        }
    }
}
