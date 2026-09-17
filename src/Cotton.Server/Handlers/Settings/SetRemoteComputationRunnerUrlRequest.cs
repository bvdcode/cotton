// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;
using Cotton.Server.Handlers.Computation;
using Cotton.Server.Models.Computation;
using Cotton.Server.Providers;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Settings
{
    public class SetRemoteComputationRunnerUrlRequest(
        string? url,
        string fallbackPublicBaseUrl) : IRequest<ComputationStatus>
    {
        public string? Url { get; } = url;
        public string FallbackPublicBaseUrl { get; } = fallbackPublicBaseUrl;
    }

    public class SetRemoteComputationRunnerUrlRequestHandler(SettingsProvider _settings, IMediator _mediator)
        : IRequestHandler<SetRemoteComputationRunnerUrlRequest, ComputationStatus>
    {
        public async Task<ComputationStatus> Handle(
            SetRemoteComputationRunnerUrlRequest request,
            CancellationToken cancellationToken)
        {
            Uri baseUri = TextEmbeddingValidation.NormalizeUrl(request.Url);
            string normalizedUrl = baseUri.AbsoluteUri.TrimEnd('/');
            ComputationStatus status = await _mediator.Send(
                new GetComputationStatusQuery(normalizedUrl, ForceRefresh: true), cancellationToken);
            if (status.Error is ComputationError error)
            {
                throw new ComputationException(error);
            }

            await _settings.UpdateSettingsAsync(
                settings =>
                {
                    settings.RemoteComputationRunnerUrl = normalizedUrl;
                    settings.ComputionMode = ComputionMode.Remote;
                },
                request.FallbackPublicBaseUrl,
                cancellationToken);
            return status;
        }
    }
}
