// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Attributes;
using Cotton.Database.Models.Enums;
using EasyExtensions.EntityFrameworkCore.Abstractions;
using EasyExtensions.Extensions;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    [Table("server_settings")]
    public class CottonServerSettings : BaseEntity<Guid>
    {
        [Column("encryption_threads")]
        public int EncryptionThreads { get; set; }

        [Column("cipher_chunk_size_bytes")]
        public int CipherChunkSizeBytes { get; set; }

        [Column("max_chunk_size_bytes")]
        public int MaxChunkSizeBytes { get; set; }

        [Column("session_timeout_hours")]
        public int SessionTimeoutHours { get; set; } = 30 * 24;

        [Column("allow_cross_user_deduplication")]
        public bool AllowCrossUserDeduplication { get; set; }

        [Column("allow_global_indexing")]
        public bool AllowGlobalIndexing { get; set; }

        [Column("telemetry_enabled")]
        public bool TelemetryEnabled { get; set; }

        [Column("timezone")]
        public string Timezone { get; set; } = null!;

        [Column("instance_id")]
        public Guid InstanceId { get; set; }

        [Column("public_base_url")]
        public string PublicBaseUrl { get; set; } = null!;

        [Column("smtp_server_address")]
        public string? SmtpServerAddress { get; set; }

        [Column("smtp_server_port")]
        public int? SmtpServerPort { get; set; }

        [Column("smtp_username")]
        public string? SmtpUsername { get; set; }

        [Column("smtp_sender_email")]
        public string? SmtpSenderEmail { get; set; }

        [Column("smtp_use_ssl")]
        public bool SmtpUseSsl { get; set; }

        [Column("s3_access_key_id")]
        public string? S3AccessKeyId { get; set; }

        [Column("s3_bucket_name")]
        public string? S3BucketName { get; set; }

        [Column("s3_region")]
        public string? S3Region { get; set; }

        [Column("s3_endpoint_url")]
        public string? S3EndpointUrl { get; set; }

        [Column("email_mode")]
        public EmailMode EmailMode { get; set; }

        [Column("compution_mode")]
        public ComputionMode ComputionMode { get; set; }

        [Column("storage_type")]
        public StorageType StorageType { get; set; }

        [Column("server_usage")]
        public ServerUsage[] ServerUsage { get; set; } = [];

        [Column("storage_space_mode")]
        public StorageSpaceMode StorageSpaceMode { get; set; }

        [Column("totp_max_failed_attempts")]
        public int TotpMaxFailedAttempts { get; set; }

        [Column("oidc_client_id")]
        public string? OidcClientId { get; set; }

        [Column("oidc_issuer")]
        public string? OidcIssuer { get; set; }

        [Encrypted]
        [Column("cloud_services_token_encrypted")]
        public string? CloudServicesTokenEncrypted { get; set; }

        [Encrypted]
        [Column("oidc_client_secret_encrypted")]
        public string? OidcClientSecretEncrypted { get; set; }

        [Encrypted]
        [Column("s3_secret_access_key_encrypted")]
        public string? S3SecretAccessKeyEncrypted { get; set; }

        [Encrypted]
        [Column("smtp_password_encrypted")]
        public string? SmtpPasswordEncrypted { get; set; }

        [Column("geo_ip_lookup_mode")]
        public GeoIpLookupMode GeoIpLookupMode { get; set; }

        [Column("custom_geo_ip_lookup_url")]
        public string? CustomGeoIpLookupUrl { get; set; }

        public string GetInstanceIdHash()
        {
            return InstanceId.ToString().Sha256();
        }
    }
}
