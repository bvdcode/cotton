// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Text;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Provides database backup key dependencies to server components.
    /// </summary>
    public class DatabaseBackupKeyProvider(CottonEncryptionSettings encryptionSettings)
    {
        /// <summary>
        /// Defines the manifest pointer logical key.
        /// </summary>
        public const string ManifestPointerLogicalKey = "database.ctn";

        /// <summary>
        /// Gets scoped pointer storage key.
        /// </summary>
        public string GetScopedPointerStorageKey()
        {
            string scopedLogicalKey = $"{ManifestPointerLogicalKey}:{encryptionSettings.MasterEncryptionKey}";
            return Hasher.ToHexStringHash(Hasher.HashData(Encoding.UTF8.GetBytes(scopedLogicalKey)));
        }
    }
}
