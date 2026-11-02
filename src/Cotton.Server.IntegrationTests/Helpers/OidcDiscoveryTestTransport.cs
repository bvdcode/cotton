// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.IntegrationTests.Helpers
{
    internal class OidcDiscoveryTestTransport : HttpMessageHandler
    {
        public const string Issuer = "https://identity.example.test";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsoluteUri != Issuer + "/.well-known/openid-configuration")
            {
                throw new InvalidOperationException("Unexpected OIDC request.");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    issuer = Issuer,
                    authorization_endpoint = Issuer + "/authorize",
                    token_endpoint = Issuer + "/token",
                }),
            });
        }
    }
}
