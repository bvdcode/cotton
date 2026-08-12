// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using Cotton.Server.Providers;
using Cotton.Storage.Processors;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route(Routes.V1.Settings)]
    [Route(Routes.V1.Server + "/settings")]
    public class StoragePipelineSettingsController(SettingsProvider settings)
        : SettingsControllerBase(settings)
    {
        private const int KiB = 1024;
        private const int MiB = 1024 * KiB;
        private static readonly int[] SupportedMaxChunkSizeBytes = [4 * MiB, 8 * MiB, 16 * MiB];
        private static readonly int[] DefaultSupportedCipherChunkSizeBytes =
        [
            Math.Max(128 * KiB, AesGcmStreamCipher.MinChunkSize),
            1 * MiB,
            4 * MiB,
            16 * MiB,
            AesGcmStreamCipher.MaxChunkSize,
        ];

        [Authorize]
        [HttpGet("chunk-size")]
        public IActionResult GetChunkSize()
        {
            return Ok(CreateChunkSizeResponse());
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("chunk-size/{maxChunkSizeBytes:int}")]
        public async Task<IActionResult> SetChunkSize(
            [FromRoute] int maxChunkSizeBytes,
            CancellationToken cancellationToken)
        {
            if (!SupportedMaxChunkSizeBytes.Contains(maxChunkSizeBytes))
            {
                return BadRequest(new
                {
                    error = "Unsupported chunk size.",
                    supportedMaxChunkSizeBytes = SupportedMaxChunkSizeBytes
                });
            }

            await Settings.SetPropertyAsync(
                x => x.MaxChunkSizeBytes,
                maxChunkSizeBytes,
                GetFallbackPublicBaseUrl(),
                cancellationToken);

            return Ok(CreateChunkSizeResponse());
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("storage-pipeline")]
        public IActionResult GetStoragePipelineSettings()
        {
            return Ok(CreateStoragePipelineResponse());
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("compression-level/{compressionLevel:int}")]
        public async Task<IActionResult> SetCompressionLevel(
            [FromRoute] int compressionLevel,
            CancellationToken cancellationToken)
        {
            try
            {
                CompressionProcessor.ThrowIfInvalidLevel(compressionLevel);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return BadRequest(new
                {
                    error = ex.Message,
                    minCompressionLevel = CompressionProcessor.MinCompressionLevel,
                    maxCompressionLevel = CompressionProcessor.MaxCompressionLevel,
                });
            }

            await Settings.SetPropertyAsync(
                x => x.CompressionLevel,
                compressionLevel,
                GetFallbackPublicBaseUrl(),
                cancellationToken);

            return Ok(CreateStoragePipelineResponse());
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("cipher-chunk-size/{cipherChunkSizeBytes:int}")]
        public async Task<IActionResult> SetCipherChunkSize(
            [FromRoute] int cipherChunkSizeBytes,
            CancellationToken cancellationToken)
        {
            if (cipherChunkSizeBytes < AesGcmStreamCipher.MinChunkSize
                || cipherChunkSizeBytes > AesGcmStreamCipher.MaxChunkSize)
            {
                return BadRequest(new
                {
                    error = "Unsupported cipher chunk size.",
                    minCipherChunkSizeBytes = AesGcmStreamCipher.MinChunkSize,
                    maxCipherChunkSizeBytes = AesGcmStreamCipher.MaxChunkSize,
                    supportedCipherChunkSizeBytes = CreateSupportedCipherChunkSizeBytes(cipherChunkSizeBytes),
                });
            }

            await Settings.SetPropertyAsync(
                x => x.CipherChunkSizeBytes,
                cipherChunkSizeBytes,
                GetFallbackPublicBaseUrl(),
                cancellationToken);

            return Ok(CreateStoragePipelineResponse());
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("encryption-threads/{encryptionThreads:int}")]
        public async Task<IActionResult> SetEncryptionThreads(
            [FromRoute] int encryptionThreads,
            CancellationToken cancellationToken)
        {
            int maxEncryptionThreads = GetMaxEncryptionThreads();
            if (encryptionThreads < 1 || encryptionThreads > maxEncryptionThreads)
            {
                return BadRequest(new
                {
                    error = "Unsupported encryption thread count.",
                    minEncryptionThreads = 1,
                    maxEncryptionThreads,
                    supportedEncryptionThreads = CreateSupportedEncryptionThreads(encryptionThreads),
                });
            }

            await Settings.SetPropertyAsync(
                x => x.EncryptionThreads,
                encryptionThreads,
                GetFallbackPublicBaseUrl(),
                cancellationToken);

            return Ok(CreateStoragePipelineResponse());
        }

        private object CreateChunkSizeResponse()
        {
            int maxChunkSizeBytes = Settings.GetServerSettings().MaxChunkSizeBytes;
            return new
            {
                maxChunkSizeBytes,
                supportedMaxChunkSizeBytes = SupportedMaxChunkSizeBytes,
            };
        }

        private object CreateStoragePipelineResponse()
        {
            ServerSettingsSnapshot settings = Settings.GetServerSettings();
            int maxEncryptionThreads = GetMaxEncryptionThreads();
            return new
            {
                settings.CompressionLevel,
                minCompressionLevel = CompressionProcessor.MinCompressionLevel,
                maxCompressionLevel = CompressionProcessor.MaxCompressionLevel,
                settings.CipherChunkSizeBytes,
                minCipherChunkSizeBytes = AesGcmStreamCipher.MinChunkSize,
                maxCipherChunkSizeBytes = AesGcmStreamCipher.MaxChunkSize,
                supportedCipherChunkSizeBytes = CreateSupportedCipherChunkSizeBytes(settings.CipherChunkSizeBytes),
                settings.EncryptionThreads,
                minEncryptionThreads = 1,
                maxEncryptionThreads,
                supportedEncryptionThreads = CreateSupportedEncryptionThreads(settings.EncryptionThreads),
            };
        }

        private static int[] CreateSupportedCipherChunkSizeBytes(int current)
        {
            return DefaultSupportedCipherChunkSizeBytes
                .Append(current)
                .Where(x => x >= AesGcmStreamCipher.MinChunkSize && x <= AesGcmStreamCipher.MaxChunkSize)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();
        }

        private static int[] CreateSupportedEncryptionThreads(int current)
        {
            int maxEncryptionThreads = GetMaxEncryptionThreads();
            return Enumerable.Range(1, maxEncryptionThreads)
                .Append(current)
                .Where(x => x >= 1 && x <= maxEncryptionThreads)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();
        }

        private static int GetMaxEncryptionThreads()
        {
            return Math.Max(1, Environment.ProcessorCount);
        }
    }
}
