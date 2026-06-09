// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net;
using Cotton.Auth;
using Cotton.Files;
using Cotton.Nodes;

namespace Cotton.Sync.Cli.Tests.TestSupport
{
    internal sealed class SyncProcessRemoteDeleteCrashHttpServer : SyncProcessCrashHttpServerBase
    {
        private readonly TaskCompletionSource _fileDeleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseDeleteResponse = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly string _expectedContentHash;
        private readonly string _expectedRelativePath;
        private readonly Guid _ownerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private readonly Guid _remoteRootId;
        private bool _fileDeletedOnRemote;

        public SyncProcessRemoteDeleteCrashHttpServer(
            Guid remoteRootId,
            string expectedRelativePath,
            string expectedContentHash)
            : base("Remote-delete crash-smoke HTTP server failed")
        {
            _remoteRootId = remoteRootId;
            _expectedRelativePath = expectedRelativePath;
            _expectedContentHash = expectedContentHash;
            Start();
        }

        public Guid RemoteFileId { get; } = Guid.Parse("33333333-3333-3333-3333-333333333333");

        public async Task WaitForFileDeletedAsync(TimeSpan timeout)
        {
            await _fileDeleted.Task.WaitAsync(timeout).ConfigureAwait(false);
        }

        public void ReleaseBlockedDeleteResponse()
        {
            _releaseDeleteResponse.TrySetResult();
        }

        protected override void ReleaseBlockedResponses()
        {
            _releaseDeleteResponse.TrySetResult();
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

            if (request.Method == HttpMethod.Post && request.PathAndQuery == "/api/v1/auth/logout?refreshToken=refresh-token")
            {
                response.StatusCode = (int)HttpStatusCode.NoContent;
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

            if (request.Method == HttpMethod.Delete
                && request.PathAndQuery == "/api/v1/files/" + RemoteFileId.ToString("D") + "?skipTrash=false")
            {
                string? ifMatch = request.GetHeader("If-Match");
                if (!string.Equals(ifMatch, "\"sha256-" + _expectedContentHash + "\"", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Unexpected delete If-Match header.");
                }

                _fileDeletedOnRemote = true;
                _fileDeleted.TrySetResult();
                await _releaseDeleteResponse.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                response.StatusCode = (int)HttpStatusCode.NoContent;
                return;
            }

            throw new InvalidOperationException("Unexpected request: " + request.Method + " " + request.PathAndQuery);
        }

        private NodeContentDto CreateRootContent()
        {
            if (_fileDeletedOnRemote)
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
                Id = RemoteFileId,
                NodeId = _remoteRootId,
                FileManifestId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                OriginalNodeFileId = RemoteFileId,
                OwnerId = _ownerId,
                Name = Path.GetFileName(_expectedRelativePath),
                ContentType = "text/plain",
                SizeBytes = 128,
                ContentHash = _expectedContentHash,
                ETag = "sha256-" + _expectedContentHash,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
        }
    }
}
