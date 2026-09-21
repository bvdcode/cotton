// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Abstractions;
using Cotton.Server.Models.Bridge;
using System.Net.Http.Headers;

namespace Cotton.Server.Services.Bridge
{
    public class BridgeAuthenticationHandler(IBridgeCredentialProvider credentials) : DelegatingHandler
    {
        private static readonly Uri BaseUri = new(global::Cotton.Constants.CottonBridgeBaseUrl);
        private static readonly Uri HealthUri = new(global::Cotton.Constants.CottonBridgeHealthUrl);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is null || !BaseUri.IsBaseOf(request.RequestUri))
            {
                throw new InvalidOperationException("Cotton Bridge credentials can only be sent to Cotton Bridge.");
            }

            if (request.Method != HttpMethod.Get || request.RequestUri != HealthUri)
            {
                BridgeCredential credential = await credentials.GetAsync(cancellationToken);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.Token);
                request.Headers.Remove(BridgeCredentialProvider.InstanceIdHeader);
                request.Headers.Add(BridgeCredentialProvider.InstanceIdHeader, credential.InstanceId.ToString());
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
