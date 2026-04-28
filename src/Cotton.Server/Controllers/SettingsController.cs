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
    }
}