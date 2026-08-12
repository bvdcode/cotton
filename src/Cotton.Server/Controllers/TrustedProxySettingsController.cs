// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route(Routes.V1.Settings)]
    [Route(Routes.V1.Server + "/settings")]
    public class TrustedProxySettingsController(
        SettingsProvider settings,
        IProxyTopologyProbeService _proxyTopologyProbe) : SettingsControllerBase(settings)
    {
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
                return Ok(CreateVerificationResponse(
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
                return Ok(CreateVerificationResponse(
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
            return Ok(CreateVerificationResponse(
                candidateProxyIpAddress,
                candidateProxyPrefixLength,
                observedProxyIpAddress,
                topology,
                matches: true,
                saved: true));
        }

        private static object CreateVerificationResponse(
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
    }
}
