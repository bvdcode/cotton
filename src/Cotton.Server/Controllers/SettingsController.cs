using Cotton.Database.Models.Enums;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route(Routes.V1.Settings)]
    [Route(Routes.V1.Server + "/settings")]
    public class SettingsController(SettingsProvider _settings) : ControllerBase
    {
        [HttpGet]
        [Authorize]
        public IActionResult GetClientSettings()
        {
            var settings = _settings.GetServerSettings();
            return Ok(new
            {
                settings.MaxChunkSizeBytes,
                SupportedHashAlgorithm = Hasher.SupportedHashAlgorithm,
            });
        }

        [HttpGet("is-setup-complete")]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public async Task<IActionResult> IsServerInitialized()
        {
            bool isServerInitialized = await _settings.IsServerInitializedAsync();
            return Ok(new { IsServerInitialized = isServerInitialized });
        }

        [Authorize]
        [HttpGet("chunk-size")]
        public IActionResult GetChunkSize()
        {
            int maxChunkSizeBytes = _settings.GetServerSettings().MaxChunkSizeBytes;
            return Ok(new { maxChunkSizeBytes });
        }

        [Authorize]
        [HttpGet("supported-hash-algorithms")]
        public IActionResult GetSupportedHashAlgorithms()
        {
            return Ok(new { supportedHashAlgorithms = new string[] { Hasher.SupportedHashAlgorithm } });
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("geoip-lookup-mode/{mode}")]
        public async Task<IActionResult> SetGeoIpLookupMode([FromRoute] GeoIpLookupMode mode, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(_settings.ValidateGeoIpLookupMode(mode));
            await _settings.SetPropertyAsync(x => x.GeoIpLookupMode, mode, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("geoip-lookup-mode")]
        public IActionResult GetGeoIpLookupMode()
        {
            GeoIpLookupMode geoIpLookupMode = _settings.GetServerSettings().GeoIpLookupMode;
            return Ok(new { geoIpLookupMode = geoIpLookupMode.ToString() });
        }

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

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("custom-geoip-lookup-url")]
        public IActionResult GetCustomGeoIpLookupUrl()
        {
            string? customGeoIpLookupUrl = _settings.GetServerSettings().CustomGeoIpLookupUrl;
            return Ok(new { customGeoIpLookupUrl });
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("server-usage")]
        public async Task<IActionResult> SetServerUsage([FromBody] JsonElement usage, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ServerUsage[] parsedUsage = ParseServerUsage(usage);
            await _settings.SetPropertyAsync(x => x.ServerUsage, parsedUsage, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("server-usage")]
        public IActionResult GetServerUsage()
        {
            string[] serverUsage = _settings.GetServerSettings().ServerUsage.Select(x => x.ToString()).ToArray();
            return Ok(new { serverUsage });
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("telemetry")]
        public async Task<IActionResult> SetTelemetry([FromBody] bool enabled, CancellationToken cancellationToken = default)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(_settings.ValidateTelemetryChange(enabled));
            await _settings.SetPropertyAsync(x => x.TelemetryEnabled, enabled, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("telemetry")]
        public IActionResult GetTelemetry()
        {
            bool telemetryEnabled = _settings.GetServerSettings().TelemetryEnabled;
            return Ok(new { telemetryEnabled });
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("storage-space-mode/{mode}")]
        public async Task<IActionResult> SetStorageSpaceMode([FromRoute] StorageSpaceMode mode, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(Enum.IsDefined(mode) ? null : "Invalid storage space mode: " + mode);
            await _settings.SetPropertyAsync(x => x.StorageSpaceMode, mode, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("storage-space-mode")]
        public IActionResult GetStorageSpaceMode()
        {
            StorageSpaceMode storageSpaceMode = _settings.GetServerSettings().StorageSpaceMode;
            return Ok(new { storageSpaceMode = storageSpaceMode.ToString() });
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("timezone")]
        public async Task<IActionResult> SetTimezone([FromBody] string? timezone, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(_settings.ValidateTimezone(timezone));
            await _settings.SetPropertyAsync(x => x.Timezone, timezone!.Trim(), GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("timezone")]
        public IActionResult GetTimezone()
        {
            string timezone = _settings.GetServerSettings().Timezone;
            return Ok(new { timezone });
        }

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

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("public-base-url")]
        public IActionResult GetPublicBaseUrl()
        {
            string? publicBaseUrl = _settings.GetServerSettings().PublicBaseUrl;
            return Ok(new { publicBaseUrl });
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("compution-mode/{mode}")]
        public async Task<IActionResult> SetComputionMode([FromRoute] ComputionMode mode, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(_settings.ValidateComputionMode(mode));
            await _settings.SetPropertyAsync(x => x.ComputionMode, mode, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("compution-mode")]
        public IActionResult GetComputionMode()
        {
            ComputionMode computionMode = _settings.GetServerSettings().ComputionMode;
            return Ok(new { computionMode = computionMode.ToString() });
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("email-mode/{mode}")]
        public async Task<IActionResult> SetEmailMode([FromRoute] EmailMode mode, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(await _settings.ValidateEmailModeAsync(mode));
            await _settings.SetPropertyAsync(x => x.EmailMode, mode, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("email-mode")]
        public IActionResult GetEmailMode()
        {
            EmailMode emailMode = _settings.GetServerSettings().EmailMode;
            return Ok(new { emailMode = emailMode.ToString() });
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("allow-cross-user-deduplication")]
        public async Task<IActionResult> SetAllowCrossUserDeduplication([FromBody] bool allow, CancellationToken cancellationToken)
        {
            await _settings.SetPropertyAsync(x => x.AllowCrossUserDeduplication, allow, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("allow-cross-user-deduplication")]
        public IActionResult GetAllowCrossUserDeduplication()
        {
            bool allowCrossUserDeduplication = _settings.GetServerSettings().AllowCrossUserDeduplication;
            return Ok(new { allowCrossUserDeduplication });
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("allow-global-indexing")]
        public async Task<IActionResult> SetAllowGlobalIndexing([FromBody] bool allow, CancellationToken cancellationToken)
        {
            await _settings.SetPropertyAsync(x => x.AllowGlobalIndexing, allow, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("allow-global-indexing")]
        public IActionResult GetAllowGlobalIndexing()
        {
            bool allowGlobalIndexing = _settings.GetServerSettings().AllowGlobalIndexing;
            return Ok(new { allowGlobalIndexing });
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("storage-type/{type}")]
        public async Task<IActionResult> SetStorageType([FromRoute] StorageType type, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(await _settings.ValidateStorageTypeAsync(type));
            await _settings.SetPropertyAsync(x => x.StorageType, type, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("storage-type")]
        public IActionResult GetStorageType()
        {
            StorageType storageType = _settings.GetServerSettings().StorageType;
            return Ok(new { storageType = storageType.ToString() });
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("s3-config")]
        [HttpPatch("set-s3-config")]
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

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("s3-config")]
        [HttpGet("get-s3-config")]
        public IActionResult GetS3Config()
        {
            var settings = _settings.GetServerSettings();
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

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("email-config")]
        [HttpPatch("set-email-config")]
        public async Task<IActionResult> SetEmailConfig([FromBody] EmailConfig emailConfig, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(_settings.ValidateEmailConfig(emailConfig));
            SettingsProvider.TryParsePort(emailConfig.Port, out int smtpPort);
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

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("email-config")]
        [HttpGet("get-email-config")]
        public IActionResult GetEmailConfig()
        {
            var settings = _settings.GetServerSettings();
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

        private async Task EnsureSettingsAsync(CancellationToken cancellationToken)
        {
            await _settings.EnsureServerSettingsAsync(GetFallbackPublicBaseUrl(), cancellationToken);
        }

        private string GetFallbackPublicBaseUrl()
        {
            return $"{Request.Scheme}://{Request.Host.Value}".TrimEnd('/');
        }

        private static void ThrowIfInvalid(string? error)
        {
            if (error is not null)
            {
                throw new BadRequestException(error);
            }
        }

        private static ServerUsage[] ParseServerUsage(JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.Array)
            {
                throw new BadRequestException("Server usage must be an array.");
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
                        throw new BadRequestException("Invalid server usage: " + raw);
                    }
                }
                else if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out int rawValue))
                {
                    usage = (ServerUsage)rawValue;
                    if (!Enum.IsDefined(usage))
                    {
                        throw new BadRequestException("Invalid server usage: " + rawValue);
                    }
                }
                else
                {
                    throw new BadRequestException("Server usage entries must be strings or numbers.");
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
