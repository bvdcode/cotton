// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net;
using System.Text;
using System.Text.Json;
using Cotton;

namespace Cotton.Sdk.Tests.Fakes
{
    internal class RotatingRefreshHttpMessageHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _secondRefreshArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _oldAccessRequestsArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _newAccessSettingsRequestCount;
        private int _oldAccessSettingsRequestCount;
        private int _refreshRequestCount;

        public int NewAccessSettingsRequestCount => Volatile.Read(ref _newAccessSettingsRequestCount);

        public int OldAccessSettingsRequestCount => Volatile.Read(ref _oldAccessSettingsRequestCount);

        public int RefreshRequestCount => Volatile.Read(ref _refreshRequestCount);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string path = request.RequestUri?.PathAndQuery ?? string.Empty;
            string? accessToken = request.Headers.Authorization?.Parameter;
            if (path == Routes.V1.Settings && accessToken == "old-access")
            {
                if (Interlocked.Increment(ref _oldAccessSettingsRequestCount) == 2)
                {
                    _oldAccessRequestsArrived.TrySetResult();
                }

                await _oldAccessRequestsArrived.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
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

            if (path != Routes.V1.Auth + "/refresh?refreshToken=old-refresh")
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            int requestNumber = Interlocked.Increment(ref _refreshRequestCount);
            if (requestNumber == 1)
            {
                Task delay = Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
                await Task.WhenAny(_secondRefreshArrived.Task, delay).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                return CreateJsonResponse(HttpStatusCode.OK, new
                {
                    accessToken = "new-access",
                    refreshToken = "new-refresh",
                });
            }

            _secondRefreshArrived.TrySetResult();
            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("Refresh token was already rotated.", Encoding.UTF8, "text/plain"),
            };
        }

        private static HttpResponseMessage CreateJsonResponse(HttpStatusCode statusCode, object payload)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload, JsonSerializerOptions.Web),
                    Encoding.UTF8,
                    "application/json"),
            };
        }
    }
}
