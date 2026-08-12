// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors
{
    public class OidcLoginStateIntegrityDescriptor : DatabaseIntegrityDescriptor<OidcLoginState>
    {
        public override string EntityName => "oidc_login_states";

        public override int SchemaVersion => 1;

        public override string GetEntityKey(OidcLoginState entity)
        {
            return entity.Id.ToString("D");
        }

        public override void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, OidcLoginState entity)
        {
            writer.WriteGuidField(nameof(entity.Id), entity.Id);
            writer.WriteGuidField(nameof(entity.ProviderId), entity.ProviderId);
            writer.WriteStringField(nameof(entity.StateHash), entity.StateHash);
            writer.WriteStringField(nameof(entity.CodeVerifierEncrypted), entity.CodeVerifierEncrypted);
            writer.WriteStringField(nameof(entity.NonceEncrypted), entity.NonceEncrypted);
            writer.WriteStringField(nameof(entity.ReturnUrl), entity.ReturnUrl);
            writer.WriteNullableGuidField(nameof(entity.LinkUserId), entity.LinkUserId);
            writer.WriteBooleanField(nameof(entity.TrustDevice), entity.TrustDevice);
            writer.WriteNullableDateTimeField(nameof(entity.ExpiresAt), entity.ExpiresAt);
        }
    }
}
