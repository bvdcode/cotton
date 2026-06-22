// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors
{
    /// <summary>
    /// Describes mobile push-device token rows protected by database integrity signing.
    /// </summary>
    /// <remarks>
    /// The MAC prevents a database-only edit from moving a registration token to another account, changing its provider
    /// identity, or restoring a revoked device token.
    /// </remarks>
    public class PushDeviceTokenIntegrityDescriptor : DatabaseIntegrityDescriptor<PushDeviceToken>
    {
        /// <inheritdoc />
        public override string EntityName => "push_device_tokens";

        /// <inheritdoc />
        public override int SchemaVersion => 1;

        /// <inheritdoc />
        public override string GetEntityKey(PushDeviceToken entity)
        {
            return entity.Id.ToString("D");
        }

        /// <inheritdoc />
        public override void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, PushDeviceToken entity)
        {
            writer.WriteGuidField(nameof(entity.Id), entity.Id);
            writer.WriteGuidField(nameof(entity.UserId), entity.UserId);
            writer.WriteInt32Field(nameof(entity.Provider), (int)entity.Provider);
            writer.WriteInt32Field(nameof(entity.Platform), (int)entity.Platform);
            writer.WriteStringField(nameof(entity.Token), entity.Token);
            writer.WriteStringField(nameof(entity.TokenHash), entity.TokenHash);
            writer.WriteStringField(nameof(entity.SessionId), entity.SessionId);
            writer.WriteStringField(nameof(entity.DeviceName), entity.DeviceName);
            writer.WriteStringField(nameof(entity.AppVersion), entity.AppVersion);
            writer.WriteNullableDateTimeField(nameof(entity.LastRegisteredAt), entity.LastRegisteredAt);
            writer.WriteNullableDateTimeField(nameof(entity.RevokedAt), entity.RevokedAt);
        }
    }
}
