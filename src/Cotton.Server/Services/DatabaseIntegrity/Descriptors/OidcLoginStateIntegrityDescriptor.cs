// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors;

/// <summary>
/// Describes short-lived OIDC login state fields protected against direct database tampering.
/// </summary>
public sealed class OidcLoginStateIntegrityDescriptor : DatabaseIntegrityDescriptor<OidcLoginState>
{
    /// <inheritdoc />
    public override string EntityName => "oidc_login_states";
    /// <inheritdoc />
    public override int SchemaVersion => 1;

    /// <inheritdoc />
    public override string GetEntityKey(OidcLoginState entity)
    {
        return entity.Id.ToString("D");
    }

    /// <inheritdoc />
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
