// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using Cotton.Storage.Abstractions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Cotton.Server.Services
{
    public class MasterKeySentinelStore(
        ILogger<MasterKeySentinelStore> _logger,
        IStorageBackend _backend)
    {
        public const string SentinelLogicalKey = "cotton.master-key.sentinel.v1";

        public static readonly string SentinelStorageKey = Hasher.ToHexStringHash(
            Hasher.HashData(Encoding.UTF8.GetBytes(SentinelLogicalKey)));

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public Task<bool> ExistsAsync() => _backend.ExistsAsync(SentinelStorageKey);

        public async Task<MasterKeySentinelResult> ValidateOrInitializeAsync(
            CottonEncryptionSettings encryptionSettings,
            CancellationToken cancellationToken = default)
        {
            using AesGcmStreamCipher cipher = CreateCipher(encryptionSettings);
            if (!await _backend.ExistsAsync(SentinelStorageKey))
            {
                await WriteNewAsync(cipher, cancellationToken);
                _logger.LogInformation("Master key sentinel created. StorageKey={StorageKey}", SentinelStorageKey);
                return MasterKeySentinelResult.Ok(created: true);
            }

            try
            {
                return await ValidateExistingAsync(cipher, cancellationToken);
            }
            catch (CryptographicException ex)
            {
                _logger.LogWarning(ex, "Master key sentinel could not be decrypted.");
                return MasterKeySentinelResult.Fail("Master key does not match this Cotton instance.");
            }
            catch (Exception ex) when (ex is InvalidDataException
                or EndOfStreamException
                or JsonException)
            {
                _logger.LogError(ex, "Master key sentinel is corrupted.");
                return MasterKeySentinelResult.Fail("Master key sentinel is corrupted.");
            }
        }

        internal static AesGcmStreamCipher CreateCipher(CottonEncryptionSettings encryptionSettings)
        {
            return StreamCipherFactory.Create(encryptionSettings);
        }

        private async Task<MasterKeySentinelResult> ValidateExistingAsync(
            AesGcmStreamCipher cipher,
            CancellationToken cancellationToken)
        {
            await using Stream encrypted = await _backend.ReadAsync(SentinelStorageKey);
            await using Stream decrypted = await cipher.DecryptAsync(
                encrypted,
                ct: cancellationToken);
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
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MasterKeySentinelPayload payload = new(
                SchemaVersion: 1,
                Purpose: SentinelLogicalKey,
                CreatedAtUtc: DateTimeOffset.UtcNow,
                Nonce: Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

            byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
            await using MemoryStream source = new(plaintext, writable: false);
            await using Stream encrypted = await cipher.EncryptAsync(
                source,
                ct: cancellationToken);
            await _backend.WriteAsync(SentinelStorageKey, encrypted);
        }

        private record MasterKeySentinelPayload(
            int SchemaVersion,
            string Purpose,
            DateTimeOffset CreatedAtUtc,
            string Nonce);
    }
}
