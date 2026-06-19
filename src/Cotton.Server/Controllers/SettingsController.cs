// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Crypto;
using Cotton.Server.Abstractions;
using Cotton.Server.Helpers;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Cotton.Storage.Processors;
using EasyExtensions;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Cotton.Server.Models;

namespace Cotton.Server.Controllers
{
    /// <summary>
    /// Exposes HTTP endpoints for settings operations.
    /// </summary>
    [ApiController]
    [Route(Routes.V1.Settings)]
    [Route(Routes.V1.Server + "/settings")]
    public class SettingsController(
        SettingsProvider _settings,
        INotificationsProvider _notifications,
        IGeoLookupService _geoLookup) : ControllerBase
    {
        private const int KiB = 1024;
        private const int MiB = 1024 * KiB;
        private static readonly int[] SupportedMaxChunkSizeBytes = [4 * MiB, 8 * MiB, 16 * MiB];
        private static readonly int[] DefaultSupportedCipherChunkSizeBytes =
        [
            Math.Max(128 * KiB, AesGcmStreamCipher.MinChunkSize),
            1 * MiB,
            4 * MiB,
            16 * MiB,
            AesGcmStreamCipher.MaxChunkSize,
        ];

        /// <summary>
        /// Gets client settings.
        /// </summary>
        [HttpGet]
        [Authorize]
        public IActionResult GetClientSettings()
        {
            CottonServerSettings settings = _settings.GetServerSettings();
            string? currentVersion = AppVersionHelpers.GetAppVersion();
            return Ok(new
            {
                Version = currentVersion,
                settings.MaxChunkSizeBytes,
                Hasher.SupportedHashAlgorithm,
            });
        }

        /// <summary>
        /// Indicates whether server initialized.
        /// </summary>
        [HttpGet("is-setup-complete")]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public async Task<IActionResult> IsServerInitialized()
        {
            bool isServerInitialized = await _settings.IsServerInitializedAsync();
            return Ok(new { IsServerInitialized = isServerInitialized });
        }

        /// <summary>
        /// Gets chunk size.
        /// </summary>
        [Authorize]
        [HttpGet("chunk-size")]
        public IActionResult GetChunkSize()
        {
            return Ok(CreateChunkSizeResponse());
        }

        /// <summary>
        /// Sets the maximum upload chunk size used by clients.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("chunk-size/{maxChunkSizeBytes:int}")]
        public async Task<IActionResult> SetChunkSize([FromRoute] int maxChunkSizeBytes, CancellationToken cancellationToken)
        {
            if (!SupportedMaxChunkSizeBytes.Contains(maxChunkSizeBytes))
            {
                return BadRequest(new
                {
                    error = "Unsupported chunk size.",
                    supportedMaxChunkSizeBytes = SupportedMaxChunkSizeBytes
                });
            }

            await _settings.SetPropertyAsync(
                x => x.MaxChunkSizeBytes,
                maxChunkSizeBytes,
                GetFallbackPublicBaseUrl(),
                cancellationToken);

            return Ok(CreateChunkSizeResponse());
        }

        private object CreateChunkSizeResponse()
        {
            int maxChunkSizeBytes = _settings.GetServerSettings().MaxChunkSizeBytes;
            return new
            {
                maxChunkSizeBytes,
                supportedMaxChunkSizeBytes = SupportedMaxChunkSizeBytes,
            };
        }

        /// <summary>
        /// Gets tunable storage pipeline settings.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("storage-pipeline")]
        public IActionResult GetStoragePipelineSettings()
        {
            return Ok(CreateStoragePipelineResponse());
        }

        /// <summary>
        /// Sets the Zstandard compression level used by future storage writes.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("compression-level/{compressionLevel:int}")]
        public async Task<IActionResult> SetCompressionLevel([FromRoute] int compressionLevel, CancellationToken cancellationToken)
        {
            try
            {
                CompressionProcessor.ThrowIfInvalidLevel(compressionLevel);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return BadRequest(new
                {
                    error = ex.Message,
                    minCompressionLevel = CompressionProcessor.MinCompressionLevel,
                    maxCompressionLevel = CompressionProcessor.MaxCompressionLevel,
                });
            }

            await _settings.SetPropertyAsync(
                x => x.CompressionLevel,
                compressionLevel,
                GetFallbackPublicBaseUrl(),
                cancellationToken);

            return Ok(CreateStoragePipelineResponse());
        }

        /// <summary>
        /// Sets the plaintext chunk size used by the AES-GCM storage encryption pipeline.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("cipher-chunk-size/{cipherChunkSizeBytes:int}")]
        public async Task<IActionResult> SetCipherChunkSize([FromRoute] int cipherChunkSizeBytes, CancellationToken cancellationToken)
        {
            if (cipherChunkSizeBytes < AesGcmStreamCipher.MinChunkSize || cipherChunkSizeBytes > AesGcmStreamCipher.MaxChunkSize)
            {
                return BadRequest(new
                {
                    error = "Unsupported cipher chunk size.",
                    minCipherChunkSizeBytes = AesGcmStreamCipher.MinChunkSize,
                    maxCipherChunkSizeBytes = AesGcmStreamCipher.MaxChunkSize,
                    supportedCipherChunkSizeBytes = CreateSupportedCipherChunkSizeBytes(cipherChunkSizeBytes),
                });
            }

            await _settings.SetPropertyAsync(
                x => x.CipherChunkSizeBytes,
                cipherChunkSizeBytes,
                GetFallbackPublicBaseUrl(),
                cancellationToken);

            return Ok(CreateStoragePipelineResponse());
        }

        /// <summary>
        /// Sets the number of AES-GCM storage encryption worker threads.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("encryption-threads/{encryptionThreads:int}")]
        public async Task<IActionResult> SetEncryptionThreads([FromRoute] int encryptionThreads, CancellationToken cancellationToken)
        {
            int maxEncryptionThreads = GetMaxEncryptionThreads();
            if (encryptionThreads < 1 || encryptionThreads > maxEncryptionThreads)
            {
                return BadRequest(new
                {
                    error = "Unsupported encryption thread count.",
                    minEncryptionThreads = 1,
                    maxEncryptionThreads,
                    supportedEncryptionThreads = CreateSupportedEncryptionThreads(encryptionThreads),
                });
            }

            await _settings.SetPropertyAsync(
                x => x.EncryptionThreads,
                encryptionThreads,
                GetFallbackPublicBaseUrl(),
                cancellationToken);

            return Ok(CreateStoragePipelineResponse());
        }

        private object CreateStoragePipelineResponse()
        {
            CottonServerSettings settings = _settings.GetServerSettings();
            int maxEncryptionThreads = GetMaxEncryptionThreads();
            return new
            {
                settings.CompressionLevel,
                minCompressionLevel = CompressionProcessor.MinCompressionLevel,
                maxCompressionLevel = CompressionProcessor.MaxCompressionLevel,
                settings.CipherChunkSizeBytes,
                minCipherChunkSizeBytes = AesGcmStreamCipher.MinChunkSize,
                maxCipherChunkSizeBytes = AesGcmStreamCipher.MaxChunkSize,
                supportedCipherChunkSizeBytes = CreateSupportedCipherChunkSizeBytes(settings.CipherChunkSizeBytes),
                settings.EncryptionThreads,
                minEncryptionThreads = 1,
                maxEncryptionThreads,
                supportedEncryptionThreads = CreateSupportedEncryptionThreads(settings.EncryptionThreads),
            };
        }

        private static int[] CreateSupportedCipherChunkSizeBytes(int current)
        {
            return DefaultSupportedCipherChunkSizeBytes
                .Append(current)
                .Where(x => x >= AesGcmStreamCipher.MinChunkSize && x <= AesGcmStreamCipher.MaxChunkSize)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();
        }

        private static int[] CreateSupportedEncryptionThreads(int current)
        {
            int maxEncryptionThreads = GetMaxEncryptionThreads();
            return Enumerable.Range(1, maxEncryptionThreads)
                .Append(current)
                .Where(x => x >= 1 && x <= maxEncryptionThreads)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();
        }

        private static int GetMaxEncryptionThreads()
        {
            return Math.Max(1, Environment.ProcessorCount);
        }

        /// <summary>
        /// Gets supported hash algorithms.
        /// </summary>
        [Authorize]
        [HttpGet("supported-hash-algorithms")]
        public IActionResult GetSupportedHashAlgorithms()
        {
            return Ok(new { supportedHashAlgorithms = new string[] { Hasher.SupportedHashAlgorithm } });
        }

        /// <summary>
        /// Sets geo ip lookup mode.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("geoip-lookup-mode/{mode}")]
        public async Task<IActionResult> SetGeoIpLookupMode([FromRoute] GeoIpLookupMode mode, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(_settings.ValidateGeoIpLookupMode(mode));
            await _settings.SetPropertyAsync(x => x.GeoIpLookupMode, mode, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Gets geo ip lookup mode.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("geoip-lookup-mode")]
        public IActionResult GetGeoIpLookupMode()
        {
            GeoIpLookupMode geoIpLookupMode = _settings.GetServerSettings().GeoIpLookupMode;
            return Ok(new { geoIpLookupMode = geoIpLookupMode.ToString() });
        }

        /// <summary>
        /// Sets custom geo ip lookup url.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("custom-geoip-lookup-url")]
        public async Task<IActionResult> SetCustomGeoIpLookupUrl([FromBody] string? url, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(_settings.ValidateCustomGeoIpLookupUrl(url));
            await _settings.SetPropertyAsync(
                x => x.CustomGeoIpLookupUrl,
                SettingsProvider.NormalizePublicBaseUrl(url),
                GetFallbackPublicBaseUrl(),
                cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Gets custom geo ip lookup url.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("custom-geoip-lookup-url")]
        public IActionResult GetCustomGeoIpLookupUrl()
        {
            string? customGeoIpLookupUrl = _settings.GetServerSettings().CustomGeoIpLookupUrl;
            return Ok(new { customGeoIpLookupUrl });
        }

        /// <summary>
        /// Tests a custom GeoIP lookup URL before saving settings.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPost("custom-geoip-lookup-url/test")]
        public async Task<IActionResult> TestCustomGeoIpLookupUrl(CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(_settings.ValidateCustomGeoIpLookupUrl(_settings.GetServerSettings().CustomGeoIpLookupUrl));
            CustomGeoLookupTestResult testResult = await _geoLookup.TestCustomLookupAsync(GetFallbackPublicBaseUrl(), cancellationToken);
            ThrowIfInvalid(testResult.Error);
            return Ok(new CustomGeoLookupTestResultDto
            {
                InputLabel = testResult.InputLabel ?? string.Empty,
                InputValue = testResult.InputValue ?? string.Empty,
                Country = testResult.Result?.Country,
                Region = testResult.Result?.Region,
                City = testResult.Result?.City,
            });
        }

        /// <summary>
        /// Sets server usage.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("server-usage")]
        public async Task<IActionResult> SetServerUsage([FromBody] JsonElement usage, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ServerUsage[] parsedUsage = ParseServerUsage(usage);
            await _settings.SetPropertyAsync(x => x.ServerUsage, parsedUsage, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Gets server usage.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("server-usage")]
        public IActionResult GetServerUsage()
        {
            string[] serverUsage = [.. _settings.GetServerSettings().ServerUsage.Select(x => x.ToString())];
            return Ok(new { serverUsage });
        }

        /// <summary>
        /// Sets telemetry.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("telemetry")]
        public async Task<IActionResult> SetTelemetry([FromBody] bool enabled, CancellationToken cancellationToken = default)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(_settings.ValidateTelemetryChange(enabled));
            await _settings.SetPropertyAsync(x => x.TelemetryEnabled, enabled, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Gets telemetry.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("telemetry")]
        public IActionResult GetTelemetry()
        {
            bool telemetryEnabled = _settings.GetServerSettings().TelemetryEnabled;
            return Ok(new { telemetryEnabled });
        }

        /// <summary>
        /// Sets storage space mode.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("storage-space-mode/{mode}")]
        public async Task<IActionResult> SetStorageSpaceMode([FromRoute] StorageSpaceMode mode, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(Enum.IsDefined(mode) ? null : "Invalid storage space mode: " + mode);
            await _settings.SetPropertyAsync(x => x.StorageSpaceMode, mode, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Gets storage space mode.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("storage-space-mode")]
        public IActionResult GetStorageSpaceMode()
        {
            StorageSpaceMode storageSpaceMode = _settings.GetServerSettings().StorageSpaceMode;
            return Ok(new { storageSpaceMode = storageSpaceMode.ToString() });
        }

        /// <summary>
        /// Sets default user storage quota bytes.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("default-user-storage-quota-bytes")]
        public async Task<IActionResult> SetDefaultUserStorageQuotaBytes([FromBody] long? quotaBytes, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(_settings.ValidateDefaultUserStorageQuotaBytes(quotaBytes));
            long? normalizedQuotaBytes = quotaBytes is null or 0 ? null : quotaBytes;
            await _settings.SetPropertyAsync(
                x => x.DefaultUserStorageQuotaBytes,
                normalizedQuotaBytes,
                GetFallbackPublicBaseUrl(),
                cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Gets default user storage quota bytes.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("default-user-storage-quota-bytes")]
        public IActionResult GetDefaultUserStorageQuotaBytes()
        {
            long? defaultUserStorageQuotaBytes = _settings.GetServerSettings().DefaultUserStorageQuotaBytes;
            return Ok(new { defaultUserStorageQuotaBytes });
        }

        /// <summary>
        /// Sets default user template node.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("default-user-template-node")]
        public async Task<IActionResult> SetDefaultUserTemplateNode([FromBody] Guid? nodeId, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            Guid? normalizedNodeId = nodeId is null || nodeId == Guid.Empty ? null : nodeId;
            Guid ownerId = User.GetUserId();
            ThrowIfInvalid(await _settings.ValidateDefaultUserTemplateNodeIdAsync(normalizedNodeId, ownerId, cancellationToken));
            await _settings.SetPropertyAsync(
                x => x.DefaultUserTemplateNodeId,
                normalizedNodeId,
                GetFallbackPublicBaseUrl(),
                cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Gets default user template node.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("default-user-template-node")]
        public IActionResult GetDefaultUserTemplateNode()
        {
            Guid? defaultUserTemplateNodeId = _settings.GetServerSettings().DefaultUserTemplateNodeId;
            return Ok(new { defaultUserTemplateNodeId });
        }

        /// <summary>
        /// Sets timezone.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("timezone")]
        public async Task<IActionResult> SetTimezone([FromBody] string? timezone, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(_settings.ValidateTimezone(timezone));
            await _settings.SetPropertyAsync(x => x.Timezone, timezone!.Trim(), GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Gets timezone.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("timezone")]
        public IActionResult GetTimezone()
        {
            string timezone = _settings.GetServerSettings().Timezone;
            return Ok(new { timezone });
        }

        /// <summary>
        /// Sets public base url.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("public-base-url")]
        public async Task<IActionResult> SetPublicBaseUrl([FromBody] string? url, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(_settings.ValidatePublicBaseUrl(url));
            await _settings.SetPropertyAsync(
                x => x.PublicBaseUrl,
                SettingsProvider.NormalizePublicBaseUrl(url),
                GetFallbackPublicBaseUrl(),
                cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Gets public base url.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("public-base-url")]
        public IActionResult GetPublicBaseUrl()
        {
            string? publicBaseUrl = _settings.GetServerSettings().PublicBaseUrl;
            return Ok(new { publicBaseUrl });
        }

        /// <summary>
        /// Sets compution mode.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("compution-mode/{mode}")]
        public async Task<IActionResult> SetComputionMode([FromRoute] ComputionMode mode, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(_settings.ValidateComputionMode(mode));
            await _settings.SetPropertyAsync(x => x.ComputionMode, mode, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Gets compution mode.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("compution-mode")]
        public IActionResult GetComputionMode()
        {
            ComputionMode computionMode = _settings.GetServerSettings().ComputionMode;
            return Ok(new { computionMode = computionMode.ToString() });
        }

        /// <summary>
        /// Sets email mode.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("email-mode/{mode}")]
        public async Task<IActionResult> SetEmailMode([FromRoute] EmailMode mode, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(await _settings.ValidateEmailModeAsync(mode));
            await _settings.SetPropertyAsync(x => x.EmailMode, mode, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Gets email mode.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("email-mode")]
        public IActionResult GetEmailMode()
        {
            EmailMode emailMode = _settings.GetServerSettings().EmailMode;
            return Ok(new { emailMode = emailMode.ToString() });
        }

        /// <summary>
        /// Sets allow cross user deduplication.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("allow-cross-user-deduplication")]
        public async Task<IActionResult> SetAllowCrossUserDeduplication([FromBody] bool allow, CancellationToken cancellationToken)
        {
            await _settings.SetPropertyAsync(x => x.AllowCrossUserDeduplication, allow, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Gets allow cross user deduplication.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("allow-cross-user-deduplication")]
        public IActionResult GetAllowCrossUserDeduplication()
        {
            bool allowCrossUserDeduplication = _settings.GetServerSettings().AllowCrossUserDeduplication;
            return Ok(new { allowCrossUserDeduplication });
        }

        /// <summary>
        /// Sets allow global indexing.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("allow-global-indexing")]
        public async Task<IActionResult> SetAllowGlobalIndexing([FromBody] bool allow, CancellationToken cancellationToken)
        {
            await _settings.SetPropertyAsync(x => x.AllowGlobalIndexing, allow, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Gets allow global indexing.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("allow-global-indexing")]
        public IActionResult GetAllowGlobalIndexing()
        {
            bool allowGlobalIndexing = _settings.GetServerSettings().AllowGlobalIndexing;
            return Ok(new { allowGlobalIndexing });
        }

        /// <summary>
        /// Sets storage type.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("storage-type/{type}")]
        public async Task<IActionResult> SetStorageType([FromRoute] StorageType type, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(await _settings.ValidateStorageTypeAsync(type));
            await _settings.SetPropertyAsync(x => x.StorageType, type, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Gets storage type.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("storage-type")]
        public IActionResult GetStorageType()
        {
            StorageType storageType = _settings.GetServerSettings().StorageType;
            return Ok(new { storageType = storageType.ToString() });
        }

        /// <summary>
        /// Sets s3 config.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("s3-config")]
        public async Task<IActionResult> SetS3Config([FromBody] S3Config s3Config, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(await _settings.ValidateS3ConfigAsync(s3Config));
            await _settings.UpdateSettingsAsync(settings =>
            {
                settings.S3AccessKeyId = s3Config.AccessKey.Trim();
                settings.S3SecretAccessKeyEncrypted = s3Config.SecretKey;
                settings.S3EndpointUrl = s3Config.Endpoint.Trim().TrimEnd('/');
                settings.S3Region = s3Config.Region.Trim();
                settings.S3BucketName = s3Config.Bucket.Trim();
            }, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Gets s3 config.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("s3-config")]
        public IActionResult GetS3Config()
        {
            CottonServerSettings settings = _settings.GetServerSettings();
            var s3Config = new S3Config
            {
                AccessKey = settings.S3AccessKeyId ?? string.Empty,
                SecretKey = string.Empty,
                Endpoint = settings.S3EndpointUrl ?? string.Empty,
                Region = settings.S3Region ?? string.Empty,
                Bucket = settings.S3BucketName ?? string.Empty
            };
            return Ok(s3Config);
        }

        /// <summary>
        /// Sets email config.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("email-config")]
        public async Task<IActionResult> SetEmailConfig([FromBody] EmailConfig emailConfig, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(_settings.ValidateEmailConfig(emailConfig));
            if (!SettingsProvider.TryParsePort(emailConfig.Port, out int smtpPort))
            {
                return this.ApiBadRequest("Invalid SMTP port number.");
            }
            await _settings.UpdateSettingsAsync(settings =>
            {
                settings.SmtpServerAddress = emailConfig.SmtpServer.Trim();
                settings.SmtpServerPort = smtpPort;
                settings.SmtpUsername = emailConfig.Username.Trim();
                settings.SmtpPasswordEncrypted = emailConfig.Password;
                settings.SmtpSenderEmail = emailConfig.FromAddress.Trim();
                settings.SmtpUseSsl = emailConfig.UseSSL;
            }, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Sends email config test.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPost("email-config/test")]
        public async Task<IActionResult> SendEmailConfigTest(CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(await _settings.ValidateEmailModeAsync(EmailMode.Custom));

            Guid userId = User.GetUserId();
            try
            {
                await _notifications.SendSmtpTestEmailAsync(userId, GetFallbackPublicBaseUrl());
            }
            catch (Exception ex)
            {
                throw new BadRequestException<CottonServerSettings>("Failed to send test email: " + ex.Message);
            }
            return NoContent();
        }

        /// <summary>
        /// Gets email config.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("email-config")]
        public IActionResult GetEmailConfig()
        {
            CottonServerSettings settings = _settings.GetServerSettings();
            var emailConfig = new EmailConfig
            {
                Username = settings.SmtpUsername ?? string.Empty,
                Password = string.Empty,
                SmtpServer = settings.SmtpServerAddress ?? string.Empty,
                Port = settings.SmtpServerPort?.ToString() ?? string.Empty,
                FromAddress = settings.SmtpSenderEmail ?? string.Empty,
                UseSSL = settings.SmtpUseSsl,
            };
            return Ok(emailConfig);
        }

        /// <summary>
        /// Sets Firebase Cloud Messaging config.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("firebase-cloud-messaging-config")]
        public async Task<IActionResult> SetFirebaseCloudMessagingConfig(
            [FromBody] FirebaseCloudMessagingConfig config,
            CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(_settings.ValidateFirebaseCloudMessagingConfig(config));
            await _settings.UpdateSettingsAsync(settings =>
            {
                settings.FcmProjectId = config.ProjectId.Trim();
                settings.FcmServiceAccountJsonEncrypted = config.ServiceAccountJson.Trim();
            }, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Gets Firebase Cloud Messaging config.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("firebase-cloud-messaging-config")]
        public IActionResult GetFirebaseCloudMessagingConfig()
        {
            var settings = _settings.GetServerSettings();
            var config = new FirebaseCloudMessagingConfig
            {
                ProjectId = settings.FcmProjectId ?? string.Empty,
                ServiceAccountJson = string.Empty,
                HasServiceAccountJson = !string.IsNullOrWhiteSpace(settings.FcmServiceAccountJsonEncrypted),
            };
            return Ok(config);
        }

        private async Task EnsureSettingsAsync(CancellationToken cancellationToken)
        {
            await _settings.EnsureServerSettingsAsync(GetFallbackPublicBaseUrl(), cancellationToken);
        }

        private string GetFallbackPublicBaseUrl()
        {
            return RequestBaseUrlHelpers.GetBaseUrl(Request);
        }

        private static void ThrowIfInvalid(string? error)
        {
            if (error is not null)
            {
                throw new BadRequestException<CottonServerSettings>(error);
            }
        }

        private static ServerUsage[] ParseServerUsage(JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.Array)
            {
                throw new BadRequestException<CottonServerSettings>("Server usage must be an array.");
            }

            var result = new List<ServerUsage>();
            foreach (JsonElement item in value.EnumerateArray())
            {
                ServerUsage usage;
                if (item.ValueKind == JsonValueKind.String)
                {
                    string? raw = item.GetString();
                    if (!Enum.TryParse(raw, ignoreCase: true, out usage))
                    {
                        throw new BadRequestException<CottonServerSettings>("Invalid server usage: " + raw);
                    }
                }
                else if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out int rawValue))
                {
                    usage = (ServerUsage)rawValue;
                    if (!Enum.IsDefined(usage))
                    {
                        throw new BadRequestException<CottonServerSettings>("Invalid server usage: " + rawValue);
                    }
                }
                else
                {
                    throw new BadRequestException<CottonServerSettings>("Server usage entries must be strings or numbers.");
                }

                if (!result.Contains(usage))
                {
                    result.Add(usage);
                }
            }

            return result.Count == 0 ? [ServerUsage.Other] : [.. result];
        }
    }
}
