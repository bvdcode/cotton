// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Minimal server-settings projection for master-key startup probes.
    /// </summary>
    [Table("server_settings")]
    internal class MasterKeyProbeServerSettings
    {
        /// <summary>
        /// Row id.
        /// </summary>
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Row creation timestamp.
        /// </summary>
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Configured storage backend type.
        /// </summary>
        [Column("storage_type")]
        public StorageType StorageType { get; set; }

        /// <summary>
        /// S3-compatible endpoint URL.
        /// </summary>
        [Column("s3_endpoint_url")]
        public string? S3EndpointUrl { get; set; }

        /// <summary>
        /// S3-compatible region.
        /// </summary>
        [Column("s3_region")]
        public string? S3Region { get; set; }

        /// <summary>
        /// S3-compatible access key id.
        /// </summary>
        [Column("s3_access_key_id")]
        public string? S3AccessKeyId { get; set; }

        /// <summary>
        /// Raw encrypted S3 secret access key.
        /// </summary>
        [Column("s3_secret_access_key_encrypted")]
        public string? S3SecretAccessKeyEncrypted { get; set; }

        /// <summary>
        /// S3-compatible bucket name.
        /// </summary>
        [Column("s3_bucket_name")]
        public string? S3BucketName { get; set; }

        /// <summary>
        /// Raw encrypted Cotton cloud services token.
        /// </summary>
        [Column("cloud_services_token_encrypted")]
        public string? CloudServicesTokenEncrypted { get; set; }

        /// <summary>
        /// Raw encrypted built-in OIDC client secret.
        /// </summary>
        [Column("oidc_client_secret_encrypted")]
        public string? OidcClientSecretEncrypted { get; set; }

        /// <summary>
        /// Raw encrypted SMTP password.
        /// </summary>
        [Column("smtp_password_encrypted")]
        public string? SmtpPasswordEncrypted { get; set; }

        /// <summary>
        /// Raw encrypted Firebase Cloud Messaging service account JSON.
        /// </summary>
        [Column("fcm_service_account_json_encrypted")]
        public string? FcmServiceAccountJsonEncrypted { get; set; }
    }
}
