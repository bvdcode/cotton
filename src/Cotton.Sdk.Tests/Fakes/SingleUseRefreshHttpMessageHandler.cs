// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net;
using System.Net.Http.Json;

namespace Cotton.Sdk.Tests.Fakes
{
    internal class SingleUseRefreshHttpMessageHandler : HttpMessageHandler
    {
        public int RefreshCount { get; private set; }

        public int RejectedRefreshCount { get; private set; }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestCount++;
            string path = request.RequestUri!.PathAndQuery;
            if (path == "/api/v1/auth/me")
            {
                return Task.FromResult(request.Headers.Authorization?.Parameter == "new-access"
                    ? CreateJsonResponse(HttpStatusCode.OK, new { id = Guid.NewGuid(), username = "account" })
                    : new HttpResponseMessage(HttpStatusCode.Unauthorized));
            }

            if (path.StartsWith("/api/v1/auth/refresh?refreshToken=", StringComparison.Ordinal))
            {
                RefreshCount++;
                if (RefreshCount == 1 && path.EndsWith("=old-refresh", StringComparison.Ordinal))
                {
                    return Task.FromResult(CreateJsonResponse(HttpStatusCode.OK, new
                    {
                        accessToken = "new-access",
                        refreshToken = "new-refresh",
                    }));
                }

                RejectedRefreshCount++;
                return Task.FromResult(CreateJsonResponse(HttpStatusCode.NotFound, new
                {
                    status = 404,
                    code = "not_found",
                    instance = "/api/v1/auth/refresh",
                    detail = "Refresh token not found or revoked.",
                }));
            }

            throw new InvalidOperationException("Unexpected request route.");
        }

        private static HttpResponseMessage CreateJsonResponse(HttpStatusCode status, object payload)
        {
            return new HttpResponseMessage(status) { Content = JsonContent.Create(payload) };
        }
    }
}
