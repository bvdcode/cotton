// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors;

/// <summary>
/// Describes server-wide settings that affect security posture and must not be silently edited in the database.
/// </summary>
public sealed class CottonServerSettingsIntegrityDescriptor : DatabaseIntegrityDescriptor<CottonServerSettings>
{
    /// <inheritdoc />
    public override string EntityName => "server_settings";
    /// <inheritdoc />
    public override int SchemaVersion => 1;

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
        writer.WriteBooleanField(nameof(entity.AllowCrossUserDeduplication), entity.AllowCrossUserDeduplication);
        writer.WriteBooleanField(nameof(entity.AllowGlobalIndexing), entity.AllowGlobalIndexing);
        writer.WriteInt32Field(nameof(entity.StorageType), (int)entity.StorageType);
        writer.WriteStringField(nameof(entity.S3AccessKeyId), entity.S3AccessKeyId);
        writer.WriteStringField(nameof(entity.S3BucketName), entity.S3BucketName);
        writer.WriteStringField(nameof(entity.S3Region), entity.S3Region);
        writer.WriteStringField(nameof(entity.S3EndpointUrl), entity.S3EndpointUrl);
        writer.WriteStringField(nameof(entity.S3SecretAccessKeyEncrypted), entity.S3SecretAccessKeyEncrypted);
        writer.WriteStringField(nameof(entity.CloudServicesTokenEncrypted), entity.CloudServicesTokenEncrypted);
        writer.WriteStringField(nameof(entity.OidcClientId), entity.OidcClientId);
        writer.WriteStringField(nameof(entity.OidcIssuer), entity.OidcIssuer);
        writer.WriteStringField(nameof(entity.OidcClientSecretEncrypted), entity.OidcClientSecretEncrypted);
        writer.WriteInt64Field(
            nameof(entity.DefaultUserStorageQuotaBytes),
            entity.DefaultUserStorageQuotaBytes ?? -1);
        writer.WriteNullableGuidField(nameof(entity.DefaultUserTemplateNodeId), entity.DefaultUserTemplateNodeId);
    }
}
