// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net;
using System.Text;
using System.Text.Json;
using Cotton.Auth;
using Cotton.Nodes;

namespace Cotton.Sync.Cli.Tests.TestSupport
{
    internal class SyncOnceDirectoryServerHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly string _expectedRelativePath;
        private readonly Guid _remoteRootId;
        private readonly bool _throwTimeoutOnChildren;

        public SyncOnceDirectoryServerHandler(
            Guid remoteRootId,
            string expectedRelativePath,
            bool throwTimeoutOnChildren = false)
        {
            _remoteRootId = remoteRootId;
            _expectedRelativePath = expectedRelativePath;
            _throwTimeoutOnChildren = throwTimeoutOnChildren;
        }

        public Guid CreatedDirectoryId { get; } = Guid.Parse("55555555-5555-5555-5555-555555555555");

        public List<HttpRequestSnapshot> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            byte[] rawBody = request.Content is null
                ? []
                : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            var snapshot = new HttpRequestSnapshot(
                request.Method,
                request.RequestUri?.PathAndQuery ?? string.Empty,
                request.Headers.Authorization?.Parameter,
                Encoding.UTF8.GetString(rawBody),
                rawBody);
            Requests.Add(snapshot);
            return CreateResponse(snapshot);
        }

        private HttpResponseMessage CreateResponse(HttpRequestSnapshot request)
        {
            if (request.Method == HttpMethod.Post && request.PathAndQuery == "/api/v1/auth/login")
            {
                return Json(HttpStatusCode.OK, new TokenPairDto
                {
                    AccessToken = "access-token",
                    RefreshToken = "refresh-token",
                });
            }

            if (request.Method == HttpMethod.Post && request.PathAndQuery == "/api/v1/auth/logout?refreshToken=refresh-token")
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            Assert.That(request.AuthorizationParameter, Is.EqualTo("access-token"));

            if (request.Method == HttpMethod.Get && request.PathAndQuery == "/api/v1/layouts/nodes/" + _remoteRootId.ToString("D"))
            {
                return Json(HttpStatusCode.OK, new NodeDto
                {
                    Id = _remoteRootId,
                    LayoutId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    ParentId = null,
                    Name = "root",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
            }

            if (request.Method == HttpMethod.Get
                && request.PathAndQuery == "/api/v1/layouts/nodes/" + _remoteRootId.ToString("D") + "/children?page=1&pageSize=100&depth=0")
            {
                if (_throwTimeoutOnChildren)
                {
                    throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.");
                }

                return Json(HttpStatusCode.OK, new NodeContentDto
                {
                    Id = _remoteRootId,
                    TotalCount = 0,
                });
            }

            if (request.Method == HttpMethod.Put && request.PathAndQuery == "/api/v1/layouts/nodes")
            {
                Assert.That(request.Body, Does.Contain("\"parentId\":\"" + _remoteRootId.ToString("D") + "\""));
                Assert.That(request.Body, Does.Contain("\"name\":\"" + Path.GetFileName(_expectedRelativePath) + "\""));
                return Json(HttpStatusCode.OK, new NodeDto
                {
                    Id = CreatedDirectoryId,
                    LayoutId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    ParentId = _remoteRootId,
                    Name = Path.GetFileName(_expectedRelativePath),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
            }

            throw new InvalidOperationException("Unexpected request: " + request.Method + " " + request.PathAndQuery);
        }

        private static HttpResponseMessage Json(HttpStatusCode statusCode, object payload)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json"),
            };
        }
    }
}
