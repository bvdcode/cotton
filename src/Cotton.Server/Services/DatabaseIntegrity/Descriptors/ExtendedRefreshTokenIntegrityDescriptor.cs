// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Database;

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors
{
    /// <summary>
    /// Describes refresh-token session rows protected by database integrity signing.
    /// </summary>
    /// <remarks>
    /// A database-only attacker must not be able to move a session to another user, restore a revoked session, or flip the
    /// authentication type/trust flag without knowing the integrity key.
    /// </remarks>
    public class ExtendedRefreshTokenIntegrityDescriptor : DatabaseIntegrityDescriptor<ExtendedRefreshToken>
    {
        /// <inheritdoc />
        public override string EntityName => "refresh_tokens";
        /// <inheritdoc />
        public override int SchemaVersion => 1;

        /// <inheritdoc />
        public override string GetEntityKey(ExtendedRefreshToken entity)
        {
            return entity.Id.ToString("D");
        }

        /// <inheritdoc />
        public override void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, ExtendedRefreshToken entity)
        {
            writer.WriteGuidField(nameof(entity.Id), entity.Id);
            writer.WriteGuidField(nameof(entity.UserId), entity.UserId);
            writer.WriteStringField(nameof(entity.Token), entity.Token);
            writer.WriteStringField(nameof(entity.SessionId), entity.SessionId);
            writer.WriteNullableDateTimeField(nameof(entity.RevokedAt), entity.RevokedAt);
            writer.WriteBooleanField(nameof(entity.IsTrusted), entity.IsTrusted);
            writer.WriteInt32Field(nameof(entity.AuthType), (int)entity.AuthType);
        }
    }
}
