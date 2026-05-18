using Cotton.Storage.Backends;
using EasyExtensions.Crypto;
using Microsoft.Extensions.Logging.Abstractions;
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
        private readonly FileSystemStorageBackend _backend;

        public MasterKeySentinelStore(ILogger<MasterKeySentinelStore> logger, string? storageBasePath = null)
        {
            _logger = logger;
            _backend = new FileSystemStorageBackend(NullLogger<FileSystemStorageBackend>.Instance, storageBasePath);
        }

        public async Task<MasterKeySentinelResult> ValidateOrInitializeAsync(
            CottonEncryptionSettings encryptionSettings,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var cipher = CreateCipher(encryptionSettings);
                if (await _backend.ExistsAsync(SentinelStorageKey))
                {
                    return await ValidateExistingAsync(cipher, cancellationToken);
                }

                await WriteNewAsync(cipher, cancellationToken);
                _logger.LogInformation("Master key sentinel created. StorageKey={StorageKey}", SentinelStorageKey);
                return MasterKeySentinelResult.Ok(created: true);
            }
            catch (CryptographicException ex)
            {
                _logger.LogWarning(ex, "Master key sentinel could not be decrypted.");
                return MasterKeySentinelResult.Fail("Master key does not match this Cotton instance.");
            }
            catch (InvalidDataException ex)
            {
                _logger.LogWarning(ex, "Master key sentinel could not be decrypted.");
                return MasterKeySentinelResult.Fail("Master key does not match this Cotton instance.");
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Master key sentinel payload is invalid.");
                return MasterKeySentinelResult.Fail("Master key sentinel is corrupted.");
            }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException or ArgumentException)
            {
                _logger.LogWarning(ex, "Master key sentinel validation failed.");
                return MasterKeySentinelResult.Fail(ex.Message);
            }
        }

        public async Task EnsureValidOrThrowAsync(
            CottonEncryptionSettings encryptionSettings,
            CancellationToken cancellationToken = default)
        {
            MasterKeySentinelResult result = await ValidateOrInitializeAsync(encryptionSettings, cancellationToken);
            if (!result.Success)
            {
                throw new InvalidOperationException(result.Error ?? "Master key sentinel validation failed.");
            }
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

        private async Task WriteNewAsync(AesGcmStreamCipher cipher, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

        private static AesGcmStreamCipher CreateCipher(CottonEncryptionSettings encryptionSettings)
        {
            if (string.IsNullOrWhiteSpace(encryptionSettings.MasterEncryptionKey))
            {
                throw new InvalidOperationException("MasterEncryptionKey is not configured.");
            }

            byte[] keyMaterial = Convert.FromBase64String(encryptionSettings.MasterEncryptionKey);
            int? threads = encryptionSettings.EncryptionThreads > 0 ? encryptionSettings.EncryptionThreads : null;
            return new AesGcmStreamCipher(keyMaterial, encryptionSettings.MasterEncryptionKeyId, threads);
        }

        private sealed record MasterKeySentinelPayload(
            int SchemaVersion,
            string Purpose,
            DateTimeOffset CreatedAtUtc,
            string Nonce);
    }

    public sealed record MasterKeySentinelResult(bool Success, bool Created, string? Error)
    {
        public static MasterKeySentinelResult Ok(bool created) => new(true, created, null);
        public static MasterKeySentinelResult Fail(string error) => new(false, false, error);
    }
}
