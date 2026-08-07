// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using Cotton.Database;
using System.Security.Cryptography;

namespace Cotton.Server.Services
{
    internal sealed class DatabaseFieldProtector : IDatabaseFieldProtector, IDisposable
    {
        private readonly AesGcmStreamCipher _cipher;
        private readonly ILogger<DatabaseFieldProtector> _logger;

        public DatabaseFieldProtector(
            CottonEncryptionSettings settings,
            ILogger<DatabaseFieldProtector> logger)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(logger);

            _cipher = StreamCipherFactory.Create(settings, encryptionThreadsOverride: 1);
            _logger = logger;
        }

        public string Protect(string plaintext)
        {
            ArgumentNullException.ThrowIfNull(plaintext);

            byte[] encryptedBytes = _cipher.EncryptString(plaintext);
            return Convert.ToBase64String(encryptedBytes);
        }

        public string Unprotect(string protectedValue)
        {
            ArgumentNullException.ThrowIfNull(protectedValue);

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(protectedValue);
                return _cipher.DecryptString(encryptedBytes);
            }
            catch (Exception ex) when (ex is FormatException or CryptographicException or InvalidDataException)
            {
                _logger.LogError(ex, "Failed to decrypt a protected database field.");
                throw;
            }
        }

        public void Dispose()
        {
            _cipher.Dispose();
        }
    }
}
