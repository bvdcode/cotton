// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net;
using System.Net.Http.Json;

namespace Cotton.Sdk.Tests.Fakes
{
    internal class BlockingRefreshHttpMessageHandler : HttpMessageHandler
    {
        private int _refreshCount;

        public TaskCompletionSource RefreshStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseRefresh { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource LogoutStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseLogout { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool PauseLogout { get; set; }
        public string? LogoutPath { get; private set; }
        public int RefreshCount => Volatile.Read(ref _refreshCount);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.AbsolutePath;
            switch (path)
            {
                case "/api/v1/auth/refresh":
                    Interlocked.Increment(ref _refreshCount);
                    RefreshStarted.TrySetResult();
                    await ReleaseRefresh.Task.WaitAsync(cancellationToken);
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new { accessToken = "new-access", refreshToken = "new-refresh" }),
                    };
                case "/api/v1/auth/me":
                    if (request.Headers.Authorization?.Parameter != "new-access")
                    {
                        return new HttpResponseMessage(HttpStatusCode.Unauthorized);
                    }

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new { id = Guid.NewGuid(), username = "account" }),
                    };
                case "/api/v1/auth/login":
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new { accessToken = "login-access", refreshToken = "login-refresh" }),
                    };
                case "/api/v1/auth/logout":
                    LogoutPath = request.RequestUri.PathAndQuery;
                    LogoutStarted.TrySetResult();
                    if (PauseLogout)
                    {
                        await ReleaseLogout.Task.WaitAsync(cancellationToken);
                    }

                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                default:
                    throw new InvalidOperationException("Unexpected request route.");
            }
        }
    }
}
