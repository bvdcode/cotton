// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net;
using System.Text;
using System.Text.Json;
using Cotton.Contracts.Auth;
using Cotton.Contracts.Files;
using Cotton.Contracts.Nodes;
using Cotton.Contracts.Settings;

namespace Cotton.Sync.Cli.Tests.TestSupport;

internal sealed class SyncOnceUploadServerHandler : HttpMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly byte[] _expectedContent;
    private readonly string _expectedContentHash;
    private readonly string _expectedRelativePath;
    private readonly Guid _ownerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _remoteRootId;

    public SyncOnceUploadServerHandler(
        Guid remoteRootId,
        string expectedRelativePath,
        string expectedContentHash,
        byte[] expectedContent)
    {
        _remoteRootId = remoteRootId;
        _expectedRelativePath = expectedRelativePath;
        _expectedContentHash = expectedContentHash;
        _expectedContent = expectedContent;
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
                Assert.That(createRequest.Validate, Is.True);
            });

            return Json(HttpStatusCode.OK, new NodeFileManifestDto
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

    private static HttpResponseMessage Text(HttpStatusCode statusCode, string body = "")
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain"),
        };
    }
}
