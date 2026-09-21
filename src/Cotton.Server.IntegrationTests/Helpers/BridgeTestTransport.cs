// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services.Bridge;
using System.Net;

namespace Cotton.Server.IntegrationTests.Helpers
{
    public class BridgeTestTransport : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.NoContent;
        public List<(string? Token, string? InstanceId)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Headers.Authorization?.Parameter,
                request.Headers.TryGetValues(BridgeCredentialProvider.InstanceIdHeader, out IEnumerable<string>? values)
                    ? values.Single() : null));
            await Task.Delay(10, cancellationToken);
            return new HttpResponseMessage(Status);
        }
    }
}
