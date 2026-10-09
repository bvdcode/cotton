// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;
using System.Text;

namespace Cotton.Server.Services
{
    public class DatabaseBackupTokenService : IDisposable
    {
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private const string Prefix = "ctb1.";
        private static readonly byte[] Purpose = Encoding.UTF8.GetBytes("Cotton.DatabaseBackup.Token.v1");
        private static readonly byte[] Permission = Encoding.UTF8.GetBytes("database-backup:create");
        private readonly byte[] _key;
        private readonly ILogger<DatabaseBackupTokenService> _logger;

        public DatabaseBackupTokenService(CottonEncryptionSettings settings, ILogger<DatabaseBackupTokenService> logger)
        {
            byte[] masterKey = Convert.FromBase64String(settings.MasterEncryptionKey);
            try
            {
                _key = KeyDerivation.DeriveSubkey(masterKey, Purpose, 32);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(masterKey);
            }
            _logger = logger;
        }

        public string Create()
        {
            byte[] token = new byte[NonceSize + TagSize + Permission.Length];
            Span<byte> nonce = token.AsSpan(0, NonceSize);
            RandomNumberGenerator.Fill(nonce);
            using AesGcm cipher = new(_key, TagSize);
            cipher.Encrypt(nonce, Permission, token.AsSpan(NonceSize + TagSize),
                token.AsSpan(NonceSize, TagSize), Purpose);
            return Prefix + WebEncoders.Base64UrlEncode(token);
        }

        public bool Validate(string token)
        {
            int byteLength = NonceSize + TagSize + Permission.Length;
            int encodedLength = (byteLength * 8 + 5) / 6;
            if (token.Length != Prefix.Length + encodedLength || !token.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return false;
            }
            try
            {
                byte[] bytes = WebEncoders.Base64UrlDecode(token[Prefix.Length..]);
                if (bytes.Length != byteLength)
                {
                    return false;
                }
                Span<byte> plaintext = stackalloc byte[Permission.Length];
                using AesGcm cipher = new(_key, TagSize);
                cipher.Decrypt(bytes.AsSpan(0, NonceSize), bytes.AsSpan(NonceSize + TagSize),
                    bytes.AsSpan(NonceSize, TagSize), plaintext, Purpose);
                return CryptographicOperations.FixedTimeEquals(plaintext, Permission);
            }
            catch (Exception exception) when (exception is FormatException or CryptographicException)
            {
                _logger.LogDebug("Rejected an invalid database backup token.");
                return false;
            }
        }

        public void Dispose() => CryptographicOperations.ZeroMemory(_key);
    }
}
