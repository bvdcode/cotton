// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors;

public sealed class UserPasskeyCredentialIntegrityDescriptor : DatabaseIntegrityDescriptor<UserPasskeyCredential>
{
    public override string EntityName => "user_passkey_credentials";
    public override int SchemaVersion => 1;

    public override string GetEntityKey(UserPasskeyCredential entity)
    {
        return entity.Id.ToString("D");
    }

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
