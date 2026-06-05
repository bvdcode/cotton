// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net;
using System.Text.Json;
using Cotton.Auth;
using Cotton.Files;
using Cotton.Nodes;
using Cotton.Settings;

namespace Cotton.Sync.Cli.Tests.TestSupport;

internal sealed class SyncProcessCrashHttpServer : SyncProcessCrashHttpServerBase
{
    private readonly TaskCompletionSource _fileCommitted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _releaseCreateResponse = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly byte[] _expectedContent;
    private readonly string _expectedContentHash;
    private readonly string _expectedRelativePath;
    private readonly Guid _ownerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _remoteRootId;
    private bool _fileCreated;

    public SyncProcessCrashHttpServer(
        Guid remoteRootId,
        string expectedRelativePath,
        string expectedContentHash,
        byte[] expectedContent)
        : base("Crash-smoke HTTP server failed")
    {
        _remoteRootId = remoteRootId;
        _expectedRelativePath = expectedRelativePath;
        _expectedContentHash = expectedContentHash;
        _expectedContent = expectedContent;
        Start();
    }

    public Guid CreatedFileId { get; } = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public async Task WaitForFileCommittedAsync(TimeSpan timeout)
    {
        await _fileCommitted.Task.WaitAsync(timeout).ConfigureAwait(false);
    }

    public void ReleaseBlockedCreateResponse()
    {
        _releaseCreateResponse.TrySetResult();
    }

    protected override void ReleaseBlockedResponses()
    {
        _releaseCreateResponse.TrySetResult();
    }

    protected override async Task WriteResponseAsync(
        HttpListenerResponse response,
        HttpRequestSnapshot request,
        CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Post && request.PathAndQuery == "/api/v1/auth/login")
        {
            await WriteJsonAsync(response, HttpStatusCode.OK, new TokenPairDto
            {
                AccessToken = "access-token",
                RefreshToken = "refresh-token",
            }, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!string.Equals(request.AuthorizationParameter, "access-token", StringComparison.Ordinal))
        {
            await WriteTextAsync(response, HttpStatusCode.Unauthorized, "Missing bearer token.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (request.Method == HttpMethod.Get && request.PathAndQuery == "/api/v1/layouts/nodes/" + _remoteRootId.ToString("D"))
        {
            await WriteJsonAsync(response, HttpStatusCode.OK, new NodeDto
            {
                Id = _remoteRootId,
                LayoutId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                ParentId = null,
                Name = "root",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (request.Method == HttpMethod.Get
            && request.PathAndQuery == "/api/v1/layouts/nodes/" + _remoteRootId.ToString("D") + "/children?page=1&pageSize=100&depth=0")
        {
            await WriteJsonAsync(response, HttpStatusCode.OK, CreateRootContent(), cancellationToken).ConfigureAwait(false);
            return;
        }

        if (request.Method == HttpMethod.Get && request.PathAndQuery == "/api/v1/settings")
        {
            await WriteJsonAsync(response, HttpStatusCode.OK, new ClientSettingsDto
            {
                Version = "test",
                MaxChunkSizeBytes = 1024,
                SupportedHashAlgorithm = "SHA-256",
            }, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (request.Method == HttpMethod.Get && request.PathAndQuery == "/api/v1/chunks/" + _expectedContentHash + "/exists")
        {
            await WriteTextAsync(response, HttpStatusCode.OK, "false", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (request.Method == HttpMethod.Post && request.PathAndQuery == "/api/v1/chunks/raw?hash=" + _expectedContentHash)
        {
            if (!request.RawBody.SequenceEqual(_expectedContent))
            {
                throw new InvalidOperationException("Unexpected uploaded chunk content.");
            }

            await WriteTextAsync(response, HttpStatusCode.Created, string.Empty, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (request.Method == HttpMethod.Post && request.PathAndQuery == "/api/v1/files/from-chunks")
        {
            CreateFileFromChunksRequestDto createRequest = JsonSerializer.Deserialize<CreateFileFromChunksRequestDto>(
                request.Body,
                JsonOptions) ?? throw new InvalidOperationException("File-create request body is missing.");
            if (createRequest.NodeId != _remoteRootId
                || !string.Equals(createRequest.Name, Path.GetFileName(_expectedRelativePath), StringComparison.Ordinal)
                || !string.Equals(createRequest.Hash, _expectedContentHash, StringComparison.Ordinal)
                || !createRequest.ChunkHashes.SequenceEqual(new[] { _expectedContentHash })
                || !createRequest.Validate)
            {
                throw new InvalidOperationException("Unexpected file-create request.");
            }

            _fileCreated = true;
            _fileCommitted.TrySetResult();
            await _releaseCreateResponse.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            await WriteJsonAsync(response, HttpStatusCode.OK, CreateManifest(), cancellationToken).ConfigureAwait(false);
            return;
        }

        throw new InvalidOperationException("Unexpected request: " + request.Method + " " + request.PathAndQuery);
    }

    private NodeContentDto CreateRootContent()
    {
        if (!_fileCreated)
        {
            return new NodeContentDto
            {
                Id = _remoteRootId,
                TotalCount = 0,
            };
        }

        return new NodeContentDto
        {
            Id = _remoteRootId,
            TotalCount = 1,
            Files = [CreateManifest()],
        };
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
}
