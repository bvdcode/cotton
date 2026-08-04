// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using Cotton.Server.Models.Dto;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Performs a bounded, unauthenticated request to the configured public endpoint to discover response-layer
    /// proxy hints through the server's own DNS path.
    /// </summary>
    public sealed class ProxyTopologyProbeService(
        HttpClient _httpClient,
        ILogger<ProxyTopologyProbeService> _logger) : IProxyTopologyProbeService
    {
        /// <inheritdoc />
        public async Task<ProxyTopologyProbeResult> DetectAsync(
            string publicBaseUrl,
            CancellationToken cancellationToken = default)
        {
            if (!Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out Uri? uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return new([], null);
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, uri);
                using HttpResponseMessage response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                return new(
                    ProxyServiceDetectionExtensions.DetectProxyServices(response),
                    ProxyServiceDetectionExtensions.DetectCloudflareMetadata(response));
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Proxy topology probe timed out for {Host}.", uri.Host);
                return new([], null);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogDebug(ex, "Proxy topology probe failed for {Host}.", uri.Host);
                return new([], null);
            }
        }
    }
}
