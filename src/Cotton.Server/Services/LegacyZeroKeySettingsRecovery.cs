// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using Cotton.Database;
using Cotton.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Security.Cryptography;

namespace Cotton.Server.Services
{
    [Obsolete("TEMPORARY 0.5 RECOVERY: remove after the upgrade window.")]
    internal class LegacyZeroKeySettingsRecovery(
        DatabaseFieldProtector _currentProtector,
        CottonEncryptionSettings encryptionSettings,
        ILogger<LegacyZeroKeySettingsRecovery> _logger) : IDatabaseFieldProtector, IDisposable
    {
        private readonly AesGcmStreamCipher _legacyCipher = new(
            new byte[AesGcmStreamCipher.KeySize],
            encryptionSettings.MasterEncryptionKeyId,
            threads: 1);
        private int _enabled = 1;
        private int _recoveredValueCount;

        public string Protect(string plaintext)
        {
            return _currentProtector.Protect(plaintext);
        }

        public string Unprotect(string protectedValue)
        {
            try
            {
                return _currentProtector.Unprotect(protectedValue);
            }
            catch (AuthenticationTagMismatchException)
            {
                if (Volatile.Read(ref _enabled) == 0
                    || !TryUnprotectWithLegacyKey(protectedValue, out string? plaintext))
                {
                    throw;
                }

                Interlocked.Increment(ref _recoveredValueCount);
                _logger.LogWarning(
                    "Recovered a database setting encrypted by the obsolete zero-key defect.");
                return plaintext;
            }
        }

        public async Task RepairAsync(
            IServiceProvider scopedServices,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(scopedServices);

            CottonDbContext dbContext = scopedServices.GetRequiredService<CottonDbContext>();
            CottonServerSettings? settings = dbContext.ChangeTracker
                .Entries<CottonServerSettings>()
                .Select(x => x.Entity)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault();
            settings ??= await dbContext.ServerSettings
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            Volatile.Write(ref _enabled, 0);
            int recoveredValueCount = Interlocked.Exchange(ref _recoveredValueCount, 0);
            if (settings is null || recoveredValueCount == 0)
            {
                return;
            }

            EntityEntry<CottonServerSettings> entry = dbContext.Entry(settings);
            MarkModifiedWhenPresent(entry.Property(x => x.CloudServicesTokenEncrypted));
            MarkModifiedWhenPresent(entry.Property(x => x.OidcClientSecretEncrypted));
            MarkModifiedWhenPresent(entry.Property(x => x.S3SecretAccessKeyEncrypted));
            MarkModifiedWhenPresent(entry.Property(x => x.SmtpPasswordEncrypted));

            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(
                "Re-encrypted {RecoveredValueCount} recovered database settings with the configured master key.",
                recoveredValueCount);
        }

        public void Dispose()
        {
            _legacyCipher.Dispose();
            GC.SuppressFinalize(this);
        }

        private bool TryUnprotectWithLegacyKey(string protectedValue, out string plaintext)
        {
            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(protectedValue);
                plaintext = _legacyCipher.DecryptString(encryptedBytes);
                return true;
            }
            catch (Exception ex) when (ex is FormatException
                or CryptographicException
                or InvalidDataException
                or EndOfStreamException)
            {
                _logger.LogDebug(ex, "The database setting is not recoverable with the obsolete zero key.");
                plaintext = string.Empty;
                return false;
            }
        }

        private static void MarkModifiedWhenPresent(PropertyEntry<CottonServerSettings, string?> property)
        {
            if (property.CurrentValue is not null)
            {
                property.IsModified = true;
            }
        }
    }
}
