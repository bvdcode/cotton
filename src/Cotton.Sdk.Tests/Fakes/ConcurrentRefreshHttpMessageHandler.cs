// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net;
using System.Text;
using System.Text.Json;
using Cotton;

namespace Cotton.Sdk.Tests.Fakes
{
    internal class ConcurrentRefreshHttpMessageHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _oldAccessRequestsArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _oldAccessSettingsRequestCount;
        private int _newAccessSettingsRequestCount;
        private int _refreshRequestCount;

        public int OldAccessSettingsRequestCount => Volatile.Read(ref _oldAccessSettingsRequestCount);

        public int NewAccessSettingsRequestCount => Volatile.Read(ref _newAccessSettingsRequestCount);

        public int RefreshRequestCount => Volatile.Read(ref _refreshRequestCount);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri?.PathAndQuery ?? string.Empty;
            string? accessToken = request.Headers.Authorization?.Parameter;
            if (path == Routes.V1.Settings && accessToken == "old-access")
            {
                if (Interlocked.Increment(ref _oldAccessSettingsRequestCount) == 2)
                {
                    _oldAccessRequestsArrived.SetResult();
                }

                await _oldAccessRequestsArrived.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("expired", Encoding.UTF8, "text/plain"),
                };
            }

            if (path == Routes.V1.Auth + "/refresh?refreshToken=old-refresh")
            {
                Interlocked.Increment(ref _refreshRequestCount);
                return CreateJsonResponse(HttpStatusCode.OK, new
                {
                    accessToken = "new-access",
                    refreshToken = "new-refresh",
                });
            }

            if (path == Routes.V1.Settings && accessToken == "new-access")
            {
                Interlocked.Increment(ref _newAccessSettingsRequestCount);
                return CreateJsonResponse(HttpStatusCode.OK, new
                {
                    version = "1.2.3",
                    maxChunkSizeBytes = 4194304,
                    supportedHashAlgorithm = "SHA256",
                });
            }

            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent($"Unexpected request {path} with access token {accessToken}.", Encoding.UTF8, "text/plain"),
            };
        }

        private static HttpResponseMessage CreateJsonResponse(HttpStatusCode statusCode, object payload)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonSerializerOptions.Web), Encoding.UTF8, "application/json"),
            };
        }
    }
}
