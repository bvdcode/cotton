// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Bridge;
using Cotton.Server.Providers;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using System.Security.Cryptography;

namespace Cotton.Server.Handlers.Bridge
{
    public record GetBridgeCredentialRequest : IRequest<BridgeCredential>;

    public class GetBridgeCredentialRequestHandler(SettingsProvider settings, ServerSettingsCache cache)
        : IRequestHandler<GetBridgeCredentialRequest, BridgeCredential>
    {
        public Task<BridgeCredential> Handle(GetBridgeCredentialRequest request, CancellationToken cancellationToken)
        {
            return cache.RunCreationExclusiveAsync(async () =>
            {
                ServerSettingsSnapshot snapshot = settings.GetServerSettings();
                if (!snapshot.TelemetryEnabled || snapshot.InstanceId == Guid.Empty)
                {
                    throw new InvalidOperationException("Cotton Bridge requires an initialized instance with telemetry enabled.");
                }

                string? token = snapshot.CloudServicesToken;
                if (string.IsNullOrEmpty(token))
                {
                    token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                    await settings.SetPropertyAsync(x => x.CloudServicesTokenEncrypted, token, cancellationToken);
                }

                return new BridgeCredential(snapshot.InstanceId, token);
            }, cancellationToken);
        }
    }
}
