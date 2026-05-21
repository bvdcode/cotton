// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using Cotton.Crypto;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Cotton.Server.Services
{
    public sealed class MasterKeySentinelStore
    {
        public const string SentinelLogicalKey = "cotton.master-key.sentinel.v1";
        public static readonly string SentinelStorageKey = Hasher.ToHexStringHash(
            Hasher.HashData(Encoding.UTF8.GetBytes(SentinelLogicalKey)));

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly ILogger<MasterKeySentinelStore> _logger;
        private readonly IStorageBackend _backend;
        private readonly IMasterKeyCompatibilityProbe? _compatibilityProbe;

        public MasterKeySentinelStore(
            ILogger<MasterKeySentinelStore> logger,
            IStorageBackend backend,
            IMasterKeyCompatibilityProbe? compatibilityProbe = null)
        {
            _logger = logger;
            _backend = backend;
            _compatibilityProbe = compatibilityProbe;
        }

        public Task<bool> ExistsAsync() => _backend.ExistsAsync(SentinelStorageKey);

        public Task<MasterKeySentinelResult> ValidateOrInitializeAsync(
            CottonEncryptionSettings encryptionSettings,
            CancellationToken cancellationToken = default) =>
            ValidateOrInitializeAsync(
                encryptionSettings,
                MasterKeySentinelInitializationMode.TrustProvidedKeyWhenNoProbe,
                cancellationToken);

        public async Task<MasterKeySentinelResult> ValidateOrInitializeAsync(
            CottonEncryptionSettings encryptionSettings,
            MasterKeySentinelInitializationMode initializationMode,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var cipher = CreateCipher(encryptionSettings);
                bool storageDependsOnEncryptedConfiguration = _backend is IStorageBackendUsesEncryptedConfiguration;

                MasterKeySentinelResult? existing = await TryValidateExistingStorageSentinelAsync(
                    encryptionSettings,
                    cipher,
                    storageDependsOnEncryptedConfiguration,
                    cancellationToken);
                if (existing is not null)
                {
                    return existing;
                }

                MasterKeyCompatibilityResult compatibility = await ValidateCompatibilityAsync(
                    encryptionSettings,
                    MasterKeyCompatibilityMode.AllowMissingEvidence,
                    cancellationToken);

                MasterKeySentinelResult? compatibilityFailure = ValidateCompatibilityResult(compatibility, initializationMode);
                if (compatibilityFailure is not null)
                {
                    return compatibilityFailure;
                }

                MasterKeySentinelResult? encryptedBackendResult = AcceptEncryptedConfigurationBackend(compatibility);
                if (encryptedBackendResult is not null)
                {
                    return encryptedBackendResult;
                }

                await WriteNewAsync(cipher, cancellationToken);
                _logger.LogInformation("Master key sentinel created. StorageKey={StorageKey}", SentinelStorageKey);
                return MasterKeySentinelResult.Ok(created: true);
            }
            catch (Exception ex) when (ex is FormatException
                or InvalidOperationException
                or ArgumentException
                or IOException
                or UnauthorizedAccessException
                or TimeoutException)
            {
                _logger.LogWarning(ex, "Master key sentinel validation failed.");
                return MasterKeySentinelResult.Fail(ex.Message);
            }
        }

        private async Task<MasterKeySentinelResult?> TryValidateExistingStorageSentinelAsync(
            CottonEncryptionSettings encryptionSettings,
            AesGcmStreamCipher cipher,
            bool storageDependsOnEncryptedConfiguration,
            CancellationToken cancellationToken)
        {
            if (storageDependsOnEncryptedConfiguration || !await _backend.ExistsAsync(SentinelStorageKey))
            {
                return null;
            }

            MasterKeySentinelResult existing = await ValidateExistingOrRepairAsync(
                encryptionSettings,
                cipher,
                cancellationToken);
            return existing;
        }

        private static MasterKeySentinelResult? ValidateCompatibilityResult(
            MasterKeyCompatibilityResult compatibility,
            MasterKeySentinelInitializationMode initializationMode)
        {
            if (!compatibility.Success)
            {
                return MasterKeySentinelResult.Fail(
                    compatibility.Error ?? "Master key could not be verified against existing Cotton data.");
            }

            if (initializationMode == MasterKeySentinelInitializationMode.RequireCompatibilityEvidenceForExistingData
                && compatibility.ExistingDataFound
                && !compatibility.EvidenceFound)
            {
                return MasterKeySentinelResult.Fail(
                    "Existing Cotton data was found, but no encrypted data could be used to verify the submitted master key. Start Cotton once with COTTON_MASTER_KEY set to the original master key so it can seed the master-key sentinel safely.");
            }

            return null;
        }

        private MasterKeySentinelResult? AcceptEncryptedConfigurationBackend(MasterKeyCompatibilityResult compatibility)
        {
            if (_backend is not IStorageBackendUsesEncryptedConfiguration)
            {
                return null;
            }

            if (compatibility.EvidenceFound || !compatibility.ExistingDataFound)
            {
                _logger.LogInformation(
                    "Master key accepted from compatibility evidence. Storage sentinel skipped because backend {BackendType} depends on encrypted configuration.",
                    _backend.GetType().Name);
                return MasterKeySentinelResult.Ok(created: false);
            }

            return MasterKeySentinelResult.Fail(
                "Existing Cotton data was found, but no encrypted data could be used to verify the submitted master key.");
        }

        private async Task<MasterKeySentinelResult> ValidateExistingOrRepairAsync(
            CottonEncryptionSettings encryptionSettings,
            AesGcmStreamCipher cipher,
            CancellationToken cancellationToken)
        {
            try
            {
                return await ValidateExistingAsync(cipher, cancellationToken);
            }
            catch (Exception ex) when (IsSentinelReadFailure(ex))
            {
                _logger.LogWarning(ex, "Master key sentinel could not be decrypted or parsed. Checking existing data before rejecting the key.");
                return await TryRepairExistingSentinelAsync(encryptionSettings, cipher, ex, cancellationToken);
            }
        }

        private async Task<MasterKeySentinelResult> TryRepairExistingSentinelAsync(
            CottonEncryptionSettings encryptionSettings,
            AesGcmStreamCipher cipher,
            Exception sentinelFailure,
            CancellationToken cancellationToken)
        {
            MasterKeyCompatibilityResult compatibility = await ValidateCompatibilityAsync(
                encryptionSettings,
                MasterKeyCompatibilityMode.RequireEvidenceForExistingData,
                cancellationToken);
            if (!compatibility.Success || !compatibility.EvidenceFound)
            {
                return sentinelFailure is JsonException
                    ? MasterKeySentinelResult.Fail("Master key sentinel is corrupted.")
                    : MasterKeySentinelResult.Fail("Master key does not match this Cotton instance.");
            }

            await WriteNewAsync(cipher, cancellationToken, overwrite: true);
            _logger.LogWarning(
                "Master key sentinel was repaired after the submitted key matched existing encrypted Cotton data. StorageKey={StorageKey}",
                SentinelStorageKey);
            return MasterKeySentinelResult.Ok(created: true, repaired: true);
        }
        private async Task<MasterKeyCompatibilityResult> ValidateCompatibilityAsync(
            CottonEncryptionSettings encryptionSettings,
            MasterKeyCompatibilityMode mode,
            CancellationToken cancellationToken)
        {
            if (_compatibilityProbe is null)
            {
                return mode == MasterKeyCompatibilityMode.RequireEvidenceForExistingData
                    ? MasterKeyCompatibilityResult.Fail(
                        "Existing Cotton data must be verified before changing the master-key sentinel, but no compatibility probe is configured.")
                    : MasterKeyCompatibilityResult.Compatible(existingDataFound: false, evidenceFound: false);
            }

            return await _compatibilityProbe.ValidateAsync(encryptionSettings, mode, cancellationToken);
        }

        private async Task<MasterKeySentinelResult> ValidateExistingAsync(
            AesGcmStreamCipher cipher,
            CancellationToken cancellationToken)
        {
            await using Stream encrypted = await _backend.ReadAsync(SentinelStorageKey);
            await using Stream decrypted = await cipher.DecryptAsync(encrypted);
            MasterKeySentinelPayload? payload = await JsonSerializer.DeserializeAsync<MasterKeySentinelPayload>(
                decrypted,
                JsonOptions,
                cancellationToken);

            if (payload is null || payload.SchemaVersion != 1 || payload.Purpose != SentinelLogicalKey)
            {
                return MasterKeySentinelResult.Fail("Master key sentinel is corrupted.");
            }

            return MasterKeySentinelResult.Ok(created: false);
        }

        private async Task WriteNewAsync(
            AesGcmStreamCipher cipher,
            CancellationToken cancellationToken,
            bool overwrite = false)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (overwrite)
            {
                await _backend.DeleteAsync(SentinelStorageKey);
            }

            var payload = new MasterKeySentinelPayload(
                SchemaVersion: 1,
                Purpose: SentinelLogicalKey,
                CreatedAtUtc: DateTimeOffset.UtcNow,
                Nonce: Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

            byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
            await using var source = new MemoryStream(plaintext, writable: false);
            await using Stream encrypted = await cipher.EncryptAsync(source);
            await _backend.WriteAsync(SentinelStorageKey, encrypted);
        }

        internal static AesGcmStreamCipher CreateCipher(CottonEncryptionSettings encryptionSettings)
        {
            if (string.IsNullOrWhiteSpace(encryptionSettings.MasterEncryptionKey))
            {
                throw new InvalidOperationException("MasterEncryptionKey is not configured.");
            }

            byte[] keyMaterial = Convert.FromBase64String(encryptionSettings.MasterEncryptionKey);
            int? threads = encryptionSettings.EncryptionThreads > 0 ? encryptionSettings.EncryptionThreads : null;
            return new AesGcmStreamCipher(keyMaterial, encryptionSettings.MasterEncryptionKeyId, threads);
        }

        private static bool IsSentinelReadFailure(Exception ex) =>
            ex is CryptographicException
                or InvalidDataException
                or JsonException;

        private sealed record MasterKeySentinelPayload(
            int SchemaVersion,
            string Purpose,
            DateTimeOffset CreatedAtUtc,
            string Nonce);
    }

    public enum MasterKeySentinelInitializationMode
    {
        TrustProvidedKeyWhenNoProbe,
        RequireCompatibilityEvidenceForExistingData
    }

    public sealed record MasterKeySentinelResult(bool Success, bool Created, bool Repaired, string? Error)
    {
        public static MasterKeySentinelResult Ok(bool created, bool repaired = false) =>
            new(true, created, repaired, null);

        public static MasterKeySentinelResult Fail(string error) => new(false, false, false, error);
    }
}
