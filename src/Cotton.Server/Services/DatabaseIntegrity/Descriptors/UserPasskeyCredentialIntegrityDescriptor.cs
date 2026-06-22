// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors
{
    /// <summary>
    /// Describes passkey credential fields protected against direct database tampering.
    /// </summary>
    /// <remarks>
    /// The signed fields are the credential binding itself: owner, credential id, public key, user handle, signature counter,
    /// authenticator identity, and backup state. The user-facing credential name is deliberately not part of the MAC.
    /// </remarks>
    public class UserPasskeyCredentialIntegrityDescriptor : DatabaseIntegrityDescriptor<UserPasskeyCredential>
    {
        /// <inheritdoc />
        public override string EntityName => "user_passkey_credentials";

        /// <inheritdoc />
        public override int SchemaVersion => 1;

        /// <inheritdoc />
        public override string GetEntityKey(UserPasskeyCredential entity)
        {
            return entity.Id.ToString("D");
        }

        /// <inheritdoc />
        public override void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, UserPasskeyCredential entity)
        {
            writer.WriteGuidField(nameof(entity.Id), entity.Id);
            writer.WriteGuidField(nameof(entity.UserId), entity.UserId);
            writer.WriteBytesField(nameof(entity.CredentialId), entity.CredentialId);
            writer.WriteBytesField(nameof(entity.PublicKey), entity.PublicKey);
            writer.WriteBytesField(nameof(entity.UserHandle), entity.UserHandle);
            writer.WriteInt64Field(nameof(entity.SignatureCounter), entity.SignatureCounter);
            writer.WriteStringArrayField(nameof(entity.Transports), entity.Transports);
            writer.WriteGuidField(nameof(entity.AaGuid), entity.AaGuid);
            writer.WriteBooleanField(nameof(entity.IsBackupEligible), entity.IsBackupEligible);
            writer.WriteBooleanField(nameof(entity.IsBackedUp), entity.IsBackedUp);
        }
    }
}
