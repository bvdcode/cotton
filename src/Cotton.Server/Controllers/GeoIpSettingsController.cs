// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Controllers
{
    /// <summary>
    /// Exposes HTTP endpoints for GeoIP settings.
    /// </summary>
    [ApiController]
    [Route(Routes.V1.Settings)]
    [Route(Routes.V1.Server + "/settings")]
    public class GeoIpSettingsController(
        SettingsProvider settings,
        ServerSettingsValidator _validator,
        IGeoLookupService _geoLookup) : SettingsControllerBase(settings)
    {
        /// <summary>
        /// Sets geo ip lookup mode.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("geoip-lookup-mode/{mode}")]
        public async Task<IActionResult> SetGeoIpLookupMode(
            [FromRoute] GeoIpLookupMode mode,
            CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(_validator.ValidateGeoIpLookupMode(mode));
            await Settings.SetPropertyAsync(
                x => x.GeoIpLookupMode,
                mode,
                GetFallbackPublicBaseUrl(),
                cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Gets geo ip lookup mode.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("geoip-lookup-mode")]
        public IActionResult GetGeoIpLookupMode()
        {
            GeoIpLookupMode geoIpLookupMode = Settings.GetServerSettings().GeoIpLookupMode;
            return Ok(new { geoIpLookupMode = geoIpLookupMode.ToString() });
        }

        /// <summary>
        /// Sets custom geo ip lookup url.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("custom-geoip-lookup-url")]
        public async Task<IActionResult> SetCustomGeoIpLookupUrl(
            [FromBody] string? url,
            CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(_validator.ValidateCustomGeoIpLookupUrl(url));
            await Settings.SetPropertyAsync(
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
            string? customGeoIpLookupUrl = Settings.GetServerSettings().CustomGeoIpLookupUrl;
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
            ThrowIfInvalid(_validator.ValidateCustomGeoIpLookupUrl(Settings.GetServerSettings().CustomGeoIpLookupUrl));
            CustomGeoLookupTestResult testResult = await _geoLookup.TestCustomLookupAsync(
                GetFallbackPublicBaseUrl(),
                cancellationToken);
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
    }
}
