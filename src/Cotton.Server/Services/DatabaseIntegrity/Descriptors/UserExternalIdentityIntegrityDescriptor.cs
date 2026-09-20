// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors
{
    public class UserExternalIdentityIntegrityDescriptor : DatabaseIntegrityDescriptor<UserExternalIdentity>
    {
        public const int LatestVersion = 1;

        public override string EntityName => "user_external_identities";

        public override int SchemaVersion => LatestVersion;

        public override string GetEntityKey(UserExternalIdentity entity)
        {
            return entity.Id.ToString("D");
        }

        public override void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, UserExternalIdentity entity, int version)
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
}
