// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Attributes;
using Cotton.Database.Models.Enums;
using EasyExtensions.EntityFrameworkCore.Abstractions;
using EasyExtensions.Extensions;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Database.Models
{
    /// <summary>Stores server-wide Cotton settings that affect storage, security, email, and integrations.</summary>
    [Table("server_settings")]
    public class CottonServerSettings : BaseEntity<Guid>
    {
        /// <summary>Configured number of encryption worker threads.</summary>
        [Column("encryption_threads")]
        public int EncryptionThreads { get; set; }

        /// <summary>Chunk size used by the cipher pipeline.</summary>
        [Column("cipher_chunk_size_bytes")]
        public int CipherChunkSizeBytes { get; set; }

        /// <summary>Maximum accepted upload chunk size.</summary>
        [Column("max_chunk_size_bytes")]
        public int MaxChunkSizeBytes { get; set; }

        /// <summary>Refresh-session lifetime in hours.</summary>
        [Column("session_timeout_hours")]
        public int SessionTimeoutHours { get; set; } = 30 * 24;

        /// <summary>Whether identical chunks may be deduplicated across different users.</summary>
        [Column("allow_cross_user_deduplication")]
        public bool AllowCrossUserDeduplication { get; set; }

        /// <summary>Whether server-side indexing is allowed globally.</summary>
        [Column("allow_global_indexing")]
        public bool AllowGlobalIndexing { get; set; }

        /// <summary>Whether optional telemetry is enabled.</summary>
        [Column("telemetry_enabled")]
        public bool TelemetryEnabled { get; set; }

        /// <summary>Server timezone identifier used for admin-facing timestamps.</summary>
        [Column("timezone")]
        public string Timezone { get; set; } = null!;

        /// <summary>Stable instance identifier generated for this Cotton installation.</summary>
        [Column("instance_id")]
        public Guid InstanceId { get; set; }

        /// <summary>Externally reachable base URL for generated links and callbacks.</summary>
        [Column("public_base_url")]
        public string PublicBaseUrl { get; set; } = null!;

        /// <summary>SMTP host used for custom email delivery.</summary>
        [Column("smtp_server_address")]
        public string? SmtpServerAddress { get; set; }

        /// <summary>SMTP port used for custom email delivery.</summary>
        [Column("smtp_server_port")]
        public int? SmtpServerPort { get; set; }

        /// <summary>SMTP username used for custom email delivery.</summary>
        [Column("smtp_username")]
        public string? SmtpUsername { get; set; }

        /// <summary>Sender email address used for outgoing messages.</summary>
        [Column("smtp_sender_email")]
        public string? SmtpSenderEmail { get; set; }

        /// <summary>Whether SMTP connections should use SSL/TLS.</summary>
        [Column("smtp_use_ssl")]
        public bool SmtpUseSsl { get; set; }

        /// <summary>S3 access key identifier for object storage.</summary>
        [Column("s3_access_key_id")]
        public string? S3AccessKeyId { get; set; }

        /// <summary>S3 bucket used for Cotton object storage.</summary>
        [Column("s3_bucket_name")]
        public string? S3BucketName { get; set; }

        /// <summary>S3 region used for object storage requests.</summary>
        [Column("s3_region")]
        public string? S3Region { get; set; }

        /// <summary>S3-compatible endpoint URL.</summary>
        [Column("s3_endpoint_url")]
        public string? S3EndpointUrl { get; set; }

        /// <summary>Configured email delivery mode.</summary>
        [Column("email_mode")]
        public EmailMode EmailMode { get; set; }

        /// <summary>Configured compute execution mode.</summary>
        [Column("compution_mode")]
        public ComputionMode ComputionMode { get; set; }

        /// <summary>Configured storage backend type.</summary>
        [Column("storage_type")]
        public StorageType StorageType { get; set; }

        /// <summary>Declared intended server usage categories.</summary>
        [Column("server_usage")]
        public ServerUsage[] ServerUsage { get; set; } = [];

        /// <summary>Configured storage-space policy mode.</summary>
        [Column("storage_space_mode")]
        public StorageSpaceMode StorageSpaceMode { get; set; }

        /// <summary>Storage quota applied to newly created users when set.</summary>
        [Column("default_user_storage_quota_bytes")]
        public long? DefaultUserStorageQuotaBytes { get; set; }

        /// <summary>Default content template node copied into newly created accounts when set.</summary>
        [Column("default_user_template_node_id")]
        public Guid? DefaultUserTemplateNodeId { get; set; }

        /// <summary>Maximum failed TOTP attempts before temporary lockout.</summary>
        [Column("totp_max_failed_attempts")]
        public int TotpMaxFailedAttempts { get; set; }

        /// <summary>OIDC client identifier.</summary>
        [Column("oidc_client_id")]
        public string? OidcClientId { get; set; }

        /// <summary>OIDC issuer URL.</summary>
        [Column("oidc_issuer")]
        public string? OidcIssuer { get; set; }

        /// <summary>Encrypted token used to access Cotton Bridge services.</summary>
        [Encrypted]
        [Column("cloud_services_token_encrypted")]
        public string? CloudServicesTokenEncrypted { get; set; }

        /// <summary>Encrypted OIDC client secret.</summary>
        [Encrypted]
        [Column("oidc_client_secret_encrypted")]
        public string? OidcClientSecretEncrypted { get; set; }

        /// <summary>Encrypted S3 secret access key.</summary>
        [Encrypted]
        [Column("s3_secret_access_key_encrypted")]
        public string? S3SecretAccessKeyEncrypted { get; set; }

        /// <summary>Encrypted SMTP password.</summary>
        [Encrypted]
        [Column("smtp_password_encrypted")]
        public string? SmtpPasswordEncrypted { get; set; }

        /// <summary>Configured geolocation lookup mode.</summary>
        [Column("geo_ip_lookup_mode")]
        public GeoIpLookupMode GeoIpLookupMode { get; set; }

        /// <summary>Custom HTTP geolocation lookup URL.</summary>
        [Column("custom_geo_ip_lookup_url")]
        public string? CustomGeoIpLookupUrl { get; set; }

        /// <summary>
        /// Returns a stable public fingerprint of <see cref="InstanceId" /> for relay and
        /// integration contracts that must not expose the raw instance identifier.
        /// </summary>
        public string GetInstanceIdHash()
        {
            return InstanceId.ToString().Sha256();
        }

        /// <summary>Resolves the configured timezone and falls back to UTC when unavailable.</summary>
        public TimeZoneInfo GetTimezoneInfo()
        {
            if (string.IsNullOrEmpty(Timezone))
            {
                return TimeZoneInfo.Utc;
            }
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(Timezone);
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Utc;
            }
        }
    }
}
