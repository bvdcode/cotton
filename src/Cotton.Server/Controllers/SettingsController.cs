using Cotton.Database.Models.Enums;
using Cotton.Server.Handlers.Server;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Models.Enums;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route(Routes.V1.Settings)]
    public class SettingsController(
        SettingsProvider _settings,
        IMediator _mediator) : ControllerBase
    {
        [HttpGet("is-setup-complete")]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public async Task<IActionResult> IsServerInitialized()
        {
            bool isServerInitialized = await _settings.IsServerInitializedAsync();
            return Ok(new { IsServerInitialized = isServerInitialized });
        }

        [HttpPost]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public async Task<IActionResult> CreateSettings(CottonServerSettingsDto request, CancellationToken cancellationToken)
        {
            string fallbackPublicBaseUrl = $"{Request.Scheme}://{Request.Host.Value}";
            await _mediator.Send(new CreateInitialServerSettingsRequest(request, fallbackPublicBaseUrl), cancellationToken);
            return Ok();
        }

        [Authorize]
        [HttpGet("chunk-size")]
        public async Task<IActionResult> GetChunkSize()
        {
            int maxChunkSizeBytes = _settings.GetServerSettings().MaxChunkSizeBytes;
            return Ok(new { maxChunkSizeBytes });
        }

        [Authorize]
        [HttpGet("supported-hash-algorithms")]
        public async Task<IActionResult> GetSupportedHashAlgorithms()
        {
            return Ok(new { supportedHashAlgorithms = new string[] { Hasher.SupportedHashAlgorithm } });
        }

        [Obsolete("This endpoint is deprecated. Use separate endpoints for each setting instead.")]
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet]
        public async Task<CottonServerSettingsDto> GetSettings()
        {
            return _settings.GetServerSettings().Adapt<CottonServerSettingsDto>();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("geoip-lookup-mode/{mode}")]
        public async Task<IActionResult> SetGeoIpLookupMode([FromRoute] GeoIpLookupMode mode, CancellationToken cancellationToken)
        {
            await _settings.SetPropertyAsync(x => x.GeoIpLookupMode, mode, cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("geoip-lookup-mode")]
        public IActionResult GetGeoIpLookupMode()
        {
            GeoIpLookupMode geoIpLookupMode = _settings.GetServerSettings().GeoIpLookupMode;
            return Ok(new { geoIpLookupMode });
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("custom-geoip-lookup-url")]
        public async Task<IActionResult> SetCustomGeoIpLookupUrl([FromBody] string url, CancellationToken cancellationToken)
        {
            await _settings.SetPropertyAsync(x => x.CustomGeoIpLookupUrl, url, cancellationToken);
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
        public async Task<IActionResult> SetServerUsage([FromBody] ServerUsage[] usage, CancellationToken cancellationToken)
        {
            await _settings.SetPropertyAsync(x => x.ServerUsage, usage, cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("server-usage")]
        public IActionResult GetServerUsage()
        {
            ServerUsage[] serverUsage = _settings.GetServerSettings().ServerUsage;
            return Ok(new { serverUsage });
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("telemetry")]
        public async Task<IActionResult> SetTelemetry([FromBody] bool enabled, CancellationToken cancellationToken = default)
        {
            await _settings.SetPropertyAsync(x => x.TelemetryEnabled, enabled, cancellationToken);
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
            await _settings.SetPropertyAsync(x => x.StorageSpaceMode, mode, cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("storage-space-mode")]
        public IActionResult GetStorageSpaceMode()
        {
            StorageSpaceMode storageSpaceMode = _settings.GetServerSettings().StorageSpaceMode;
            return Ok(new { storageSpaceMode });
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("timezone")]
        public async Task<IActionResult> SetTimezone([FromBody] string timezone, CancellationToken cancellationToken)
        {
            await _settings.SetPropertyAsync(x => x.Timezone, timezone, cancellationToken);
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
        public async Task<IActionResult> SetPublicBaseUrl([FromBody] string url, CancellationToken cancellationToken)
        {
            await _settings.SetPropertyAsync(x => x.PublicBaseUrl, url, cancellationToken);
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
            await _settings.SetPropertyAsync(x => x.ComputionMode, mode, cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("compution-mode")]
        public IActionResult GetComputionMode()
        {
            ComputionMode computionMode = _settings.GetServerSettings().ComputionMode;
            return Ok(new { computionMode });
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("email-mode/{mode}")]
        public async Task<IActionResult> SetEmailMode([FromRoute] EmailMode mode, CancellationToken cancellationToken)
        {
            await _settings.SetPropertyAsync(x => x.EmailMode, mode, cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("email-mode")]
        public IActionResult GetEmailMode()
        {
            EmailMode emailMode = _settings.GetServerSettings().EmailMode;
            return Ok(new { emailMode });
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("allow-cross-user-deduplication")]
        public async Task<IActionResult> SetAllowCrossUserDeduplication([FromBody] bool allow, CancellationToken cancellationToken)
        {
            await _settings.SetPropertyAsync(x => x.AllowCrossUserDeduplication, allow, cancellationToken);
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
            await _settings.SetPropertyAsync(x => x.AllowGlobalIndexing, allow, cancellationToken);
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
        [HttpPatch("set-s3-config")]
        public async Task<IActionResult> SetS3Config([FromBody] S3Config s3Config, CancellationToken cancellationToken)
        {
            await _settings.SetPropertyAsync(x => x.S3AccessKeyId, s3Config.AccessKey, cancellationToken);
            await _settings.SetPropertyAsync(x => x.S3SecretAccessKeyEncrypted, s3Config.SecretKey, cancellationToken);
            await _settings.SetPropertyAsync(x => x.S3EndpointUrl, s3Config.Endpoint, cancellationToken);
            await _settings.SetPropertyAsync(x => x.S3Region, s3Config.Region, cancellationToken);
            await _settings.SetPropertyAsync(x => x.S3BucketName, s3Config.Bucket, cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
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
        [HttpPatch("set-email-config")]
        public async Task<IActionResult> SetEmailConfig([FromBody] EmailConfig emailConfig, CancellationToken cancellationToken)
        {
            await _settings.SetPropertyAsync(x => x.SmtpUseSsl = emailConfig.UseSSL, cancellationToken);
            // todo: finish
            return NoContent();
        }
    }
}