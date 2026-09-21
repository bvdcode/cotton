// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;
using Cotton.Server.Models.Computation;
using Cotton.Server.Providers;
using Cotton.Server.Services.Computation;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Computation
{
    public record GetComputationServiceInfoQuery(string? Url = null) : IRequest<ComputationServiceInfo>;

    public class GetComputationServiceInfoQueryHandler(SettingsProvider settings, TeiClient client)
        : IRequestHandler<GetComputationServiceInfoQuery, ComputationServiceInfo>
    {
        public async Task<ComputationServiceInfo> Handle(
            GetComputationServiceInfoQuery request, CancellationToken cancellationToken)
        {
            string? url = request.Url;
            if (url is null)
            {
                url = ResolveConfiguredUrl(settings.GetServerSettings());
            }

            Uri baseUri = TextEmbeddingValidation.NormalizeUrl(url);
            ComputationServiceInfo info = await client.GetServiceInfoAsync(baseUri, cancellationToken);
            TextEmbeddingValidation.ValidateInfo(info);
            return info;
        }

        internal static string ResolveConfiguredUrl(ServerSettingsSnapshot snapshot)
        {
            switch (snapshot.ComputionMode)
            {
                case ComputionMode.Remote:
                    if (string.IsNullOrWhiteSpace(snapshot.RemoteComputationRunnerUrl))
                    {
                        throw new ComputationException(ComputationError.NotConfigured);
                    }
                    return snapshot.RemoteComputationRunnerUrl;
                case ComputionMode.Cloud:
                    if (!snapshot.TelemetryEnabled)
                    {
                        throw new ComputationException(ComputationError.NotConfigured);
                    }
                    return global::Cotton.Constants.CottonBridgeBaseUrl;
                case ComputionMode.Local:
                    throw new ComputationException(ComputationError.UnsupportedMode);
                default:
                    throw new ComputationException(ComputationError.UnsupportedMode);
            }
        }
    }
}
