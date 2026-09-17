// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Providers;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Settings
{
    public class SetRemoteComputationRunnerUrlRequest(
        string? url,
        string fallbackPublicBaseUrl) : IRequest
    {
        public string? Url { get; } = url;
        public string FallbackPublicBaseUrl { get; } = fallbackPublicBaseUrl;
    }

    public class SetRemoteComputationRunnerUrlRequestHandler(SettingsProvider _settings)
        : IRequestHandler<SetRemoteComputationRunnerUrlRequest>
    {
        public async Task Handle(
            SetRemoteComputationRunnerUrlRequest request,
            CancellationToken cancellationToken)
        {
            if (!SettingsProvider.TryNormalizePublicBaseUrl(request.Url, out string normalizedUrl))
            {
                throw new BadRequestException<CottonServerSettings>(
                    "Remote computation runner URL must be an absolute HTTP or HTTPS URL.");
            }

            await _settings.SetPropertyAsync(
                x => x.RemoteComputationRunnerUrl,
                normalizedUrl,
                request.FallbackPublicBaseUrl,
                cancellationToken);
        }
    }
}
