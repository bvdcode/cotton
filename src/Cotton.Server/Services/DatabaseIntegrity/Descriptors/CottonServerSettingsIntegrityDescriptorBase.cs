// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using System.Globalization;

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors
{
    /// <summary>
    /// Base descriptor for server-wide settings integrity schema versions.
    /// </summary>
    public abstract class CottonServerSettingsIntegrityDescriptorBase :
        DatabaseIntegrityDescriptor<CottonServerSettings>
    {
        /// <inheritdoc />
        public override string EntityName => "server_settings";

        /// <summary>
        /// Gets a value indicating whether compression settings participate in the canonical payload.
        /// </summary>
        protected abstract bool IncludeCompressionLevel { get; }

        /// <summary>
        /// Gets a value indicating whether Firebase Cloud Messaging settings participate in the canonical payload.
        /// </summary>
        protected abstract bool IncludeFirebaseCloudMessagingFields { get; }

        /// <inheritdoc />
        public override string GetEntityKey(CottonServerSettings entity)
        {
            return entity.Id.ToString("D");
        }

        /// <inheritdoc />
        public override void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, CottonServerSettings entity)
        {
            writer.WriteGuidField(nameof(entity.Id), entity.Id);
            writer.WriteGuidField(nameof(entity.InstanceId), entity.InstanceId);
            writer.WriteStringField(nameof(entity.PublicBaseUrl), entity.PublicBaseUrl);
            writer.WriteInt32Field(nameof(entity.EncryptionThreads), entity.EncryptionThreads);
            writer.WriteInt32Field(nameof(entity.CipherChunkSizeBytes), entity.CipherChunkSizeBytes);
            if (IncludeCompressionLevel)
            {
                writer.WriteInt32Field(nameof(entity.CompressionLevel), entity.CompressionLevel);
            }

            writer.WriteInt32Field(nameof(entity.MaxChunkSizeBytes), entity.MaxChunkSizeBytes);
            writer.WriteInt32Field(nameof(entity.SessionTimeoutHours), entity.SessionTimeoutHours);
            writer.WriteBooleanField(nameof(entity.TelemetryEnabled), entity.TelemetryEnabled);
            writer.WriteStringField(nameof(entity.Timezone), entity.Timezone);
            writer.WriteBooleanField(nameof(entity.AllowCrossUserDeduplication), entity.AllowCrossUserDeduplication);
            writer.WriteBooleanField(nameof(entity.AllowGlobalIndexing), entity.AllowGlobalIndexing);
            writer.WriteInt32Field(nameof(entity.EmailMode), (int)entity.EmailMode);
            writer.WriteStringField(nameof(entity.SmtpServerAddress), entity.SmtpServerAddress);
            writer.WriteInt32Field(nameof(entity.SmtpServerPort), entity.SmtpServerPort ?? -1);
            writer.WriteStringField(nameof(entity.SmtpUsername), entity.SmtpUsername);
            writer.WriteStringField(nameof(entity.SmtpSenderEmail), entity.SmtpSenderEmail);
            writer.WriteBooleanField(nameof(entity.SmtpUseSsl), entity.SmtpUseSsl);
            writer.WriteStringField(nameof(entity.SmtpPasswordEncrypted), entity.SmtpPasswordEncrypted);
            writer.WriteInt32Field(nameof(entity.ComputionMode), (int)entity.ComputionMode);
            writer.WriteInt32Field(nameof(entity.StorageType), (int)entity.StorageType);
            writer.WriteInt32Field(nameof(entity.StorageSpaceMode), (int)entity.StorageSpaceMode);
            writer.WriteStringArrayField(
                nameof(entity.ServerUsage),
                entity.ServerUsage
                    .Select(x => ((int)x).ToString(CultureInfo.InvariantCulture))
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToArray());
            writer.WriteStringField(nameof(entity.S3AccessKeyId), entity.S3AccessKeyId);
            writer.WriteStringField(nameof(entity.S3BucketName), entity.S3BucketName);
            writer.WriteStringField(nameof(entity.S3Region), entity.S3Region);
            writer.WriteStringField(nameof(entity.S3EndpointUrl), entity.S3EndpointUrl);
            writer.WriteStringField(nameof(entity.S3SecretAccessKeyEncrypted), entity.S3SecretAccessKeyEncrypted);
            writer.WriteStringField(nameof(entity.CloudServicesTokenEncrypted), entity.CloudServicesTokenEncrypted);
            writer.WriteStringField(nameof(entity.OidcClientId), entity.OidcClientId);
            writer.WriteStringField(nameof(entity.OidcIssuer), entity.OidcIssuer);
            writer.WriteStringField(nameof(entity.OidcClientSecretEncrypted), entity.OidcClientSecretEncrypted);
            if (IncludeFirebaseCloudMessagingFields)
            {
                writer.WriteStringField(nameof(entity.FcmProjectId), entity.FcmProjectId);
                writer.WriteStringField(
                    nameof(entity.FcmServiceAccountJsonEncrypted),
                    entity.FcmServiceAccountJsonEncrypted);
            }

            writer.WriteInt32Field(nameof(entity.TotpMaxFailedAttempts), entity.TotpMaxFailedAttempts);
            writer.WriteInt32Field(nameof(entity.GeoIpLookupMode), (int)entity.GeoIpLookupMode);
            writer.WriteStringField(nameof(entity.CustomGeoIpLookupUrl), entity.CustomGeoIpLookupUrl);
            writer.WriteInt64Field(
                nameof(entity.DefaultUserStorageQuotaBytes),
                entity.DefaultUserStorageQuotaBytes ?? -1);
            writer.WriteNullableGuidField(nameof(entity.DefaultUserTemplateNodeId), entity.DefaultUserTemplateNodeId);
        }
    }
}
