// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors;

/// <summary>
/// Describes external identity links protected against direct database tampering.
/// </summary>
public sealed class UserExternalIdentityIntegrityDescriptor : DatabaseIntegrityDescriptor<UserExternalIdentity>
{
    /// <inheritdoc />
    public override string EntityName => "user_external_identities";
    /// <inheritdoc />
    public override int SchemaVersion => 1;

    /// <inheritdoc />
    public override string GetEntityKey(UserExternalIdentity entity)
    {
        return entity.Id.ToString("D");
    }

    /// <inheritdoc />
    public override void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, UserExternalIdentity entity)
    {
        writer.WriteGuidField(nameof(entity.Id), entity.Id);
        writer.WriteGuidField(nameof(entity.UserId), entity.UserId);
        writer.WriteGuidField(nameof(entity.ProviderId), entity.ProviderId);
        writer.WriteStringField(nameof(entity.Issuer), entity.Issuer);
        writer.WriteStringField(nameof(entity.Subject), entity.Subject);
        writer.WriteStringField(nameof(entity.Email), entity.Email);
        writer.WriteBooleanField(nameof(entity.EmailVerified), entity.EmailVerified);
        writer.WriteStringField(nameof(entity.DisplayName), entity.DisplayName);
        writer.WriteStringField(nameof(entity.PictureUrl), entity.PictureUrl);
    }
}
