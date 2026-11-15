// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;
using System.Text;

namespace Cotton.Server.Services
{
    public class DatabaseBackupTokenService(CottonEncryptionSettings settings, ILogger<DatabaseBackupTokenService> logger)
        : ProtectedTokenService(settings, "Cotton.DatabaseBackup.Token.v1", "ctb1.", Permission.Length, logger)
    {
        private static readonly byte[] Permission = Encoding.UTF8.GetBytes("database-backup:create");

        public string Create() => Protect(Permission);

        public bool Validate(string token)
        {
            Span<byte> plaintext = stackalloc byte[Permission.Length];
            return TryUnprotect(token, plaintext) && CryptographicOperations.FixedTimeEquals(plaintext, Permission);
        }
    }
}
