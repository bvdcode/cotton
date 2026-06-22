// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors
{
    /// <summary>
    /// Describes OIDC provider settings protected against direct database tampering.
    /// </summary>
    public sealed class OidcProviderIntegrityDescriptor : DatabaseIntegrityDescriptor<OidcProvider>
    {
        /// <inheritdoc />
        public override string EntityName => "oidc_providers";
        /// <inheritdoc />
        public override int SchemaVersion => 1;

        /// <inheritdoc />
        public override string GetEntityKey(OidcProvider entity)
        {
            return entity.Id.ToString("D");
        }

        /// <inheritdoc />
        public override void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, OidcProvider entity)
        {
            writer.WriteGuidField(nameof(entity.Id), entity.Id);
            writer.WriteStringField(nameof(entity.Name), entity.Name);
            writer.WriteStringField(nameof(entity.Slug), entity.Slug);
            writer.WriteStringField(nameof(entity.Issuer), entity.Issuer);
            writer.WriteStringField(nameof(entity.ClientId), entity.ClientId);
            writer.WriteStringField(nameof(entity.ClientSecretEncrypted), entity.ClientSecretEncrypted);
            writer.WriteStringArrayField(nameof(entity.Scopes), entity.Scopes);
            writer.WriteBooleanField(nameof(entity.IsEnabled), entity.IsEnabled);
            writer.WriteBooleanField(nameof(entity.AllowAccountCreation), entity.AllowAccountCreation);
            writer.WriteBooleanField(nameof(entity.RequireVerifiedEmail), entity.RequireVerifiedEmail);
            writer.WriteInt32Field(nameof(entity.DefaultRole), (int)entity.DefaultRole);
            writer.WriteStringArrayField(nameof(entity.AllowedEmailDomains), entity.AllowedEmailDomains);
            writer.WriteBooleanField(nameof(entity.SyncProfile), entity.SyncProfile);
            writer.WriteBooleanField(nameof(entity.SyncAvatar), entity.SyncAvatar);
        }
    }
}
