// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net;
using Cotton.Shared.Contracts.Auth;
using Cotton.Shared.Contracts.Files;
using Cotton.Shared.Contracts.Nodes;

namespace Cotton.Sync.Cli.Tests.TestSupport;

internal sealed class SyncProcessDownloadCrashHttpServer : SyncProcessCrashHttpServerBase
{
    private readonly byte[] _content;
    private readonly TaskCompletionSource _firstDownloadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _releaseFirstDownload = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly string _contentHash;
    private readonly string _relativePath;
    private readonly Guid _ownerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _remoteRootId;
    private bool _firstDownloadWasBlocked;

    public SyncProcessDownloadCrashHttpServer(
        Guid remoteRootId,
        string relativePath,
        string contentHash,
        byte[] content)
        : base("Download crash-smoke HTTP server failed")
    {
        _remoteRootId = remoteRootId;
        _relativePath = relativePath;
        _contentHash = contentHash;
        _content = content;
        Start();
    }

    public Guid RemoteFileId { get; } = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public async Task WaitForFirstDownloadStartedAsync(TimeSpan timeout)
    {
        await _firstDownloadStarted.Task.WaitAsync(timeout).ConfigureAwait(false);
    }

    public void ReleaseFirstDownload()
    {
        _releaseFirstDownload.TrySetResult();
    }

    protected override void ReleaseBlockedResponses()
    {
        _releaseFirstDownload.TrySetResult();
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
            await WriteJsonAsync(response, HttpStatusCode.OK, new NodeContentDto
            {
                Id = _remoteRootId,
                TotalCount = 1,
                Files = [CreateManifest()],
            }, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (request.Method == HttpMethod.Get
            && request.PathAndQuery == "/api/v1/files/" + RemoteFileId.ToString("D") + "/content?download=false")
        {
            await WriteContentAsync(response, cancellationToken).ConfigureAwait(false);
            return;
        }

        throw new InvalidOperationException("Unexpected request: " + request.Method + " " + request.PathAndQuery);
    }

    private async Task WriteContentAsync(HttpListenerResponse response, CancellationToken cancellationToken)
    {
        response.StatusCode = (int)HttpStatusCode.OK;
        response.ContentType = "text/plain";
        if (!_firstDownloadWasBlocked)
        {
            _firstDownloadWasBlocked = true;
            int partialLength = Math.Max(1, _content.Length / 2);
            await response.OutputStream.WriteAsync(_content.AsMemory(0, partialLength), cancellationToken).ConfigureAwait(false);
            await response.OutputStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            _firstDownloadStarted.TrySetResult();
            await _releaseFirstDownload.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            await response.OutputStream.WriteAsync(
                _content.AsMemory(partialLength, _content.Length - partialLength),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        response.ContentLength64 = _content.Length;
        await response.OutputStream.WriteAsync(_content, cancellationToken).ConfigureAwait(false);
    }

    private NodeFileManifestDto CreateManifest()
    {
        return new NodeFileManifestDto
        {
            Id = RemoteFileId,
            NodeId = _remoteRootId,
            FileManifestId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            OriginalNodeFileId = RemoteFileId,
            OwnerId = _ownerId,
            Name = Path.GetFileName(_relativePath),
            ContentType = "text/plain",
            SizeBytes = _content.Length,
            ContentHash = _contentHash,
            ETag = "sha256-" + _contentHash,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }
}
