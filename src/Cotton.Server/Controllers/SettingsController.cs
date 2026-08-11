// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using Cotton.Server.Helpers;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using EasyExtensions;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
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
        SettingsProvider settings,
        ServerSettingsValidator _validator,
        INotificationsProvider _notifications,
        IProxyTopologyProbeService _proxyTopologyProbe) : SettingsControllerBase(settings)
    {

        /// <summary>
        /// Gets client settings.
        /// </summary>
        [HttpGet]
        [Authorize]
        public IActionResult GetClientSettings()
        {
            ServerSettingsSnapshot settings = Settings.GetServerSettings();
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
            bool isServerInitialized = await Settings.IsServerInitializedAsync();
            return Ok(new { IsServerInitialized = isServerInitialized });
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
        /// Sets server usage.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("server-usage")]
        public async Task<IActionResult> SetServerUsage([FromBody] JsonElement usage, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ServerUsage[] parsedUsage = ParseServerUsage(usage);
            await Settings.SetPropertyAsync(x => x.ServerUsage, parsedUsage, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Gets server usage.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("server-usage")]
        public IActionResult GetServerUsage()
        {
            string[] serverUsage = [.. Settings.GetServerSettings().ServerUsage.Select(x => x.ToString())];
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
            ThrowIfInvalid(_validator.ValidateTelemetryChange(enabled));
            await Settings.SetPropertyAsync(x => x.TelemetryEnabled, enabled, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Gets telemetry.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("telemetry")]
        public IActionResult GetTelemetry()
        {
            bool telemetryEnabled = Settings.GetServerSettings().TelemetryEnabled;
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
            await Settings.SetPropertyAsync(x => x.StorageSpaceMode, mode, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Gets storage space mode.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("storage-space-mode")]
        public IActionResult GetStorageSpaceMode()
        {
            StorageSpaceMode storageSpaceMode = Settings.GetServerSettings().StorageSpaceMode;
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
            ThrowIfInvalid(_validator.ValidateDefaultUserStorageQuotaBytes(quotaBytes));
            long? normalizedQuotaBytes = quotaBytes is null or 0 ? null : quotaBytes;
            await Settings.SetPropertyAsync(
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
            long? defaultUserStorageQuotaBytes = Settings.GetServerSettings().DefaultUserStorageQuotaBytes;
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
            ThrowIfInvalid(await _validator.ValidateDefaultUserTemplateNodeIdAsync(normalizedNodeId, ownerId, cancellationToken));
            await Settings.SetPropertyAsync(
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
            Guid? defaultUserTemplateNodeId = Settings.GetServerSettings().DefaultUserTemplateNodeId;
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
            ThrowIfInvalid(_validator.ValidateTimezone(timezone));
            await Settings.SetPropertyAsync(x => x.Timezone, timezone!.Trim(), GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Gets timezone.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("timezone")]
        public IActionResult GetTimezone()
        {
            string timezone = Settings.GetServerSettings().Timezone;
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
            ThrowIfInvalid(_validator.ValidatePublicBaseUrl(url));
            await Settings.SetPropertyAsync(
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
            string? publicBaseUrl = Settings.GetServerSettings().PublicBaseUrl;
            return Ok(new { publicBaseUrl });
        }

        /// <summary>
        /// Gets the configured immediate reverse-proxy address.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("trusted-proxy-ip-address")]
        public IActionResult GetTrustedProxyIpAddress()
        {
            ServerSettingsSnapshot settings = Settings.GetServerSettings();
            IPAddress? configuredProxy = settings.TrustedProxyIpAddress;
            string? trustedProxyIpAddress = configuredProxy is null
                ? null
                : TrustedProxyRequestExtensions.FormatConfiguredProxy(
                    configuredProxy,
                    settings.TrustedProxyPrefixLength);
            return Ok(new { trustedProxyIpAddress });
        }

        /// <summary>
        /// Gets the address of the peer that opened the current connection for trusted-proxy auto-detection.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("trusted-proxy-ip-address/observed")]
        public async Task<IActionResult> GetObservedProxyIpAddress(CancellationToken cancellationToken)
        {
            IPAddress? observedProxy = Request.GetConnectingIPAddress();
            string? observedProxyIpAddress = observedProxy?.ToString();
            string? suggestedTrustedProxy = observedProxy is null
                ? null
                : TrustedProxyRequestExtensions.GetSuggestedProxyConfiguration(observedProxy);
            ProxyTopologyProbeResult topology = await DetectProxyTopologyAsync(cancellationToken);
            return Ok(new
            {
                observedProxyIpAddress,
                suggestedTrustedProxy,
                detectedProxyServices = topology.Services,
                cloudflare = topology.Cloudflare,
            });
        }

        /// <summary>
        /// Selects direct-connection mode, or verifies an immediate reverse-proxy address and saves it on success.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPost("trusted-proxy-ip-address/verify-and-save")]
        public async Task<IActionResult> VerifyAndSaveTrustedProxyIpAddress(
            [FromBody] string? ipAddress,
            CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ProxyTopologyProbeResult topology = await DetectProxyTopologyAsync(cancellationToken);

            IPAddress? observedProxyIpAddress = Request.GetConnectingIPAddress();
            if (observedProxyIpAddress is null)
            {
                return this.ApiBadRequest("The connecting proxy IP address is unavailable for this request.");
            }

            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                await Settings.UpdateSettingsAsync(
                    settings =>
                    {
                        settings.TrustedProxyIpAddress = null;
                        settings.TrustedProxyPrefixLength = null;
                    },
                    GetFallbackPublicBaseUrl(),
                    cancellationToken);
                return Ok(CreateTrustedProxyVerificationResponse(
                    configuredProxyIpAddress: null,
                    configuredProxyPrefixLength: null,
                    observedProxyIpAddress,
                    topology,
                    matches: true,
                    saved: true));
            }

            if (!TrustedProxyRequestExtensions.TryParseTrustedProxy(
                    ipAddress.Trim(),
                    out IPAddress candidateProxyIpAddress,
                    out byte? candidateProxyPrefixLength))
            {
                return this.ApiBadRequest(
                    "Trusted proxy must be a valid IPv4, IPv6, or CIDR network.");
            }

            bool matches = TrustedProxyRequestExtensions.IsDirectConnectionMode(
                    candidateProxyIpAddress,
                    candidateProxyPrefixLength)
                || TrustedProxyRequestExtensions.MatchesTrustedProxy(
                    candidateProxyIpAddress,
                    candidateProxyPrefixLength,
                    observedProxyIpAddress);
            if (!matches)
            {
                return Ok(CreateTrustedProxyVerificationResponse(
                    candidateProxyIpAddress,
                    candidateProxyPrefixLength,
                    observedProxyIpAddress,
                    topology,
                    matches: false,
                    saved: false));
            }

            await Settings.UpdateSettingsAsync(
                settings =>
                {
                    settings.TrustedProxyIpAddress = candidateProxyIpAddress;
                    settings.TrustedProxyPrefixLength = candidateProxyPrefixLength;
                },
                GetFallbackPublicBaseUrl(),
                cancellationToken);
            return Ok(CreateTrustedProxyVerificationResponse(
                candidateProxyIpAddress,
                candidateProxyPrefixLength,
                observedProxyIpAddress,
                topology,
                matches: true,
                saved: true));
        }

        /// <summary>
        /// Sets compution mode.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("compution-mode/{mode}")]
        public async Task<IActionResult> SetComputionMode([FromRoute] ComputionMode mode, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(_validator.ValidateComputionMode(mode));
            await Settings.SetPropertyAsync(x => x.ComputionMode, mode, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Gets compution mode.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("compution-mode")]
        public IActionResult GetComputionMode()
        {
            ComputionMode computionMode = Settings.GetServerSettings().ComputionMode;
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
            ThrowIfInvalid(await _validator.ValidateEmailModeAsync(mode, cancellationToken));
            await Settings.SetPropertyAsync(x => x.EmailMode, mode, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Gets email mode.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("email-mode")]
        public IActionResult GetEmailMode()
        {
            EmailMode emailMode = Settings.GetServerSettings().EmailMode;
            return Ok(new { emailMode = emailMode.ToString() });
        }

        /// <summary>
        /// Sets allow cross user deduplication.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("allow-cross-user-deduplication")]
        public async Task<IActionResult> SetAllowCrossUserDeduplication([FromBody] bool allow, CancellationToken cancellationToken)
        {
            await Settings.SetPropertyAsync(x => x.AllowCrossUserDeduplication, allow, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Gets allow cross user deduplication.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("allow-cross-user-deduplication")]
        public IActionResult GetAllowCrossUserDeduplication()
        {
            bool allowCrossUserDeduplication = Settings.GetServerSettings().AllowCrossUserDeduplication;
            return Ok(new { allowCrossUserDeduplication });
        }

        /// <summary>
        /// Sets allow global indexing.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("allow-global-indexing")]
        public async Task<IActionResult> SetAllowGlobalIndexing([FromBody] bool allow, CancellationToken cancellationToken)
        {
            await Settings.SetPropertyAsync(x => x.AllowGlobalIndexing, allow, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Gets allow global indexing.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("allow-global-indexing")]
        public IActionResult GetAllowGlobalIndexing()
        {
            bool allowGlobalIndexing = Settings.GetServerSettings().AllowGlobalIndexing;
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
            ThrowIfInvalid(await _validator.ValidateStorageTypeAsync(type, cancellationToken));
            await Settings.SetPropertyAsync(x => x.StorageType, type, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Gets storage type.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("storage-type")]
        public IActionResult GetStorageType()
        {
            StorageType storageType = Settings.GetServerSettings().StorageType;
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
            ThrowIfInvalid(await _validator.ValidateS3ConfigAsync(s3Config, cancellationToken));
            await Settings.UpdateSettingsAsync(settings =>
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
            ServerSettingsSnapshot settings = Settings.GetServerSettings();
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
            ThrowIfInvalid(_validator.ValidateEmailConfig(emailConfig));
            if (!ServerSettingsValidator.TryParsePort(emailConfig.Port, out int smtpPort))
            {
                return this.ApiBadRequest("Invalid SMTP port number.");
            }
            await Settings.UpdateSettingsAsync(settings =>
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
            ThrowIfInvalid(await _validator.ValidateEmailModeAsync(EmailMode.Custom, cancellationToken));

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
            ServerSettingsSnapshot settings = Settings.GetServerSettings();
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


        private static object CreateTrustedProxyVerificationResponse(
            IPAddress? configuredProxyIpAddress,
            byte? configuredProxyPrefixLength,
            IPAddress observedProxyIpAddress,
            ProxyTopologyProbeResult topology,
            bool matches,
            bool saved)
        {
            return new
            {
                trustedProxyIpAddress = configuredProxyIpAddress is null
                    ? null
                    : TrustedProxyRequestExtensions.FormatConfiguredProxy(
                        configuredProxyIpAddress,
                        configuredProxyPrefixLength),
                observedProxyIpAddress = observedProxyIpAddress.ToString(),
                detectedProxyServices = topology.Services,
                cloudflare = topology.Cloudflare,
                matches,
                saved,
            };
        }

        private async Task<ProxyTopologyProbeResult> DetectProxyTopologyAsync(
            CancellationToken cancellationToken)
        {
            IReadOnlyList<string> requestServices = Request.DetectProxyServices();
            string publicBaseUrl = Settings.GetServerSettings().PublicBaseUrl;
            ProxyTopologyProbeResult probe = await _proxyTopologyProbe.DetectAsync(
                publicBaseUrl,
                cancellationToken);
            return new(
                ProxyServiceDetectionExtensions.MergeProxyServices(requestServices, probe.Services),
                ProxyServiceDetectionExtensions.MergeCloudflareMetadata(
                    Request.DetectCloudflareMetadata(),
                    probe.Cloudflare));
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
