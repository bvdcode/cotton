// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net;
using System.Text;
using System.Text.Json;
using Cotton.Auth;
using Cotton.Files;
using Cotton.Nodes;
using Cotton.Settings;

namespace Cotton.Sync.Cli.Tests.TestSupport
{
    internal class SyncOnceUploadServerHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly byte[] _expectedContent;
        private readonly string _expectedContentHash;
        private readonly string _expectedRelativePath;
        private readonly bool _exposeCreatedFileInChildren;
        private readonly bool _allowAppCodeAuth;
        private readonly bool _expireAccessTokenBeforeChunkExists;
        private readonly Guid _ownerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private readonly Guid _remoteRootId;
        private int _appCodeStartNetworkFailuresRemaining;
        private bool _fileCreated;
        private bool _chunkExistsExpiredOnce;
        private bool _refreshed;

        public SyncOnceUploadServerHandler(
            Guid remoteRootId,
            string expectedRelativePath,
            string expectedContentHash,
            byte[] expectedContent,
            bool exposeCreatedFileInChildren = true,
            bool allowAppCodeAuth = false,
            bool expireAccessTokenBeforeChunkExists = false,
            int appCodeStartNetworkFailuresBeforeSuccess = 0)
        {
            _remoteRootId = remoteRootId;
            _expectedRelativePath = expectedRelativePath;
            _expectedContentHash = expectedContentHash;
            _expectedContent = expectedContent;
            _exposeCreatedFileInChildren = exposeCreatedFileInChildren;
            _allowAppCodeAuth = allowAppCodeAuth;
            _expireAccessTokenBeforeChunkExists = expireAccessTokenBeforeChunkExists;
            _appCodeStartNetworkFailuresRemaining = appCodeStartNetworkFailuresBeforeSuccess;
        }

        public Guid CreatedFileId { get; } = Guid.Parse("33333333-3333-3333-3333-333333333333");

        public List<HttpRequestSnapshot> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            byte[] rawBody = request.Content is null
                ? []
                : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            string body = Encoding.UTF8.GetString(rawBody);
            var snapshot = new HttpRequestSnapshot(
                request.Method,
                request.RequestUri?.PathAndQuery ?? string.Empty,
                request.Headers.Authorization?.Parameter,
                body,
                rawBody);
            Requests.Add(snapshot);
            return CreateResponse(snapshot);
        }

        private HttpResponseMessage CreateResponse(HttpRequestSnapshot request)
        {
            if (request.Method == HttpMethod.Post && request.PathAndQuery == "/api/v1/auth/login")
            {
                Assert.That(request.Body, Does.Contain("\"username\":\"testuser\""));
                Assert.That(request.Body, Does.Contain("\"password\":\"testpassword\""));
                Assert.That(request.Body, Does.Contain("\"trustDevice\":true"));
                return Json(HttpStatusCode.OK, new TokenPairDto
                {
                    AccessToken = "access-token",
                    RefreshToken = "refresh-token",
                });
            }

            if (request.Method == HttpMethod.Post && request.PathAndQuery == "/api/v1/auth/refresh?refreshToken=refresh-token")
            {
                _refreshed = true;
                return Json(HttpStatusCode.OK, new TokenPairDto
                {
                    AccessToken = "refreshed-access-token",
                    RefreshToken = "refreshed-refresh-token",
                });
            }

            if (_allowAppCodeAuth
                && request.Method == HttpMethod.Post
                && request.PathAndQuery == "/api/v1/oauth/app-code/start")
            {
                if (_appCodeStartNetworkFailuresRemaining > 0)
                {
                    _appCodeStartNetworkFailuresRemaining--;
                    throw new HttpRequestException("Firewall blocked app-code start.");
                }

                return Json(HttpStatusCode.OK, new
                {
                    approvalId = "0190a000-0000-7000-8000-000000000022",
                    approvalUrl = "/oauth/app-code/0190a000-0000-7000-8000-000000000022",
                    pollToken = "poll-token",
                    expiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
                    pollIntervalSeconds = 1,
                });
            }

            if (_allowAppCodeAuth
                && request.Method == HttpMethod.Post
                && request.PathAndQuery == "/api/v1/oauth/app-code/poll")
            {
                Assert.That(request.Body, Does.Contain("\"pollToken\":\"poll-token\""));
                return Json(HttpStatusCode.OK, new TokenPairDto
                {
                    AccessToken = "access-token",
                    RefreshToken = "refresh-token",
                });
            }

            if (_allowAppCodeAuth
                && request.Method == HttpMethod.Get
                && request.PathAndQuery == "/api/v1/auth/me")
            {
                return Json(HttpStatusCode.OK, new UserDto
                {
                    Id = _ownerId,
                    Username = "browser",
                    Email = "browser@example.test",
                });
            }

            if (request.Method == HttpMethod.Post && request.PathAndQuery == "/api/v1/auth/logout?refreshToken=refresh-token")
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (request.Method == HttpMethod.Post && request.PathAndQuery == "/api/v1/auth/logout?refreshToken=refreshed-refresh-token")
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (_expireAccessTokenBeforeChunkExists
                && !_chunkExistsExpiredOnce
                && request.Method == HttpMethod.Get
                && request.PathAndQuery == "/api/v1/chunks/" + _expectedContentHash + "/exists")
            {
                Assert.That(request.AuthorizationParameter, Is.EqualTo("access-token"));
                _chunkExistsExpiredOnce = true;
                return Text(HttpStatusCode.Unauthorized, "expired access token");
            }

            Assert.That(request.AuthorizationParameter, Is.EqualTo(_refreshed ? "refreshed-access-token" : "access-token"));

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
                if (_fileCreated && _exposeCreatedFileInChildren)
                {
                    return Json(HttpStatusCode.OK, new NodeContentDto
                    {
                        Id = _remoteRootId,
                        TotalCount = 1,
                        Files = [CreateManifest()],
                    });
                }

                return Json(HttpStatusCode.OK, new NodeContentDto
                {
                    Id = _remoteRootId,
                    TotalCount = 0,
                });
            }

            if (request.Method == HttpMethod.Get && request.PathAndQuery == "/api/v1/settings")
            {
                return Json(HttpStatusCode.OK, new ClientSettingsDto
                {
                    Version = "test",
                    MaxChunkSizeBytes = 1024,
                    SupportedHashAlgorithm = "SHA-256",
                });
            }

            if (request.Method == HttpMethod.Get && request.PathAndQuery == "/api/v1/chunks/" + _expectedContentHash + "/exists")
            {
                return Text(HttpStatusCode.OK, "false");
            }

            if (request.Method == HttpMethod.Post && request.PathAndQuery == "/api/v1/chunks/raw?hash=" + _expectedContentHash)
            {
                Assert.That(request.RawBody, Is.EqualTo(_expectedContent));
                return Text(HttpStatusCode.Created);
            }

            if (request.Method == HttpMethod.Post && request.PathAndQuery == "/api/v1/files/from-chunks")
            {
                CreateFileFromChunksRequestDto createRequest = JsonSerializer.Deserialize<CreateFileFromChunksRequestDto>(
                    request.Body,
                    JsonOptions)!;
                Assert.Multiple(() =>
                {
                    Assert.That(createRequest.NodeId, Is.EqualTo(_remoteRootId));
                    Assert.That(createRequest.Name, Is.EqualTo(Path.GetFileName(_expectedRelativePath)));
                    Assert.That(createRequest.Hash, Is.EqualTo(_expectedContentHash));
                    Assert.That(createRequest.ChunkHashes, Is.EqualTo(new[] { _expectedContentHash }));
                    Assert.That(createRequest.Validate, Is.False);
                });

                _fileCreated = true;
                return Json(HttpStatusCode.OK, CreateManifest());
            }

            if (request.Method == HttpMethod.Get
                && request.PathAndQuery == "/api/v1/files/" + CreatedFileId.ToString("D") + "/content?download=false")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(_expectedContent),
                };
            }

            throw new InvalidOperationException("Unexpected request: " + request.Method + " " + request.PathAndQuery);
        }

        private NodeFileManifestDto CreateManifest()
        {
            return new NodeFileManifestDto
            {
                Id = CreatedFileId,
                NodeId = _remoteRootId,
                FileManifestId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                OriginalNodeFileId = CreatedFileId,
                OwnerId = _ownerId,
                Name = Path.GetFileName(_expectedRelativePath),
                ContentType = "text/plain",
                SizeBytes = _expectedContent.Length,
                ContentHash = _expectedContentHash,
                ETag = "sha256-" + _expectedContentHash,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
        }

        private static HttpResponseMessage Json(HttpStatusCode statusCode, object payload)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json"),
            };
        }

        private static HttpResponseMessage Text(HttpStatusCode statusCode, string body = "")
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "text/plain"),
            };
        }
    }
}
