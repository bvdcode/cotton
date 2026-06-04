// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Cotton.Contracts.Auth;
using Cotton.Contracts.Files;
using Cotton.Contracts.Nodes;
using Cotton.Contracts.Settings;

namespace Cotton.Sync.Cli.Tests.TestSupport;

internal sealed class SyncProcessCrashHttpServer : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly TaskCompletionSource _fileCommitted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _releaseCreateResponse = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _stop = new();
    private readonly ConcurrentQueue<Exception> _faults = new();
    private readonly byte[] _expectedContent;
    private readonly string _expectedContentHash;
    private readonly string _expectedRelativePath;
    private readonly HttpListener _listener = new();
    private readonly Guid _ownerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _remoteRootId;
    private readonly List<HttpRequestSnapshot> _requests = [];
    private readonly object _gate = new();
    private readonly Task _listenTask;
    private bool _fileCreated;

    public SyncProcessCrashHttpServer(
        Guid remoteRootId,
        string expectedRelativePath,
        string expectedContentHash,
        byte[] expectedContent)
    {
        _remoteRootId = remoteRootId;
        _expectedRelativePath = expectedRelativePath;
        _expectedContentHash = expectedContentHash;
        _expectedContent = expectedContent;
        BaseUri = new Uri("http://127.0.0.1:" + GetFreePort().ToStringInvariant() + "/");
        _listener.Prefixes.Add(BaseUri.AbsoluteUri);
        _listener.Start();
        _listenTask = ListenAsync();
    }

    public Uri BaseUri { get; }

    public Guid CreatedFileId { get; } = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public IReadOnlyList<HttpRequestSnapshot> Requests
    {
        get
        {
            lock (_gate)
            {
                return _requests.ToList();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _releaseCreateResponse.TrySetResult();
        _stop.Cancel();
        _listener.Close();
        try
        {
            await _listenTask.ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (HttpListenerException)
        {
        }

        _stop.Dispose();
    }

    public async Task WaitForFileCommittedAsync(TimeSpan timeout)
    {
        await _fileCommitted.Task.WaitAsync(timeout).ConfigureAwait(false);
    }

    public void ReleaseBlockedCreateResponse()
    {
        _releaseCreateResponse.TrySetResult();
    }

    public void AssertNoFaults()
    {
        if (_faults.TryPeek(out Exception? fault))
        {
            throw new AssertionException("Crash-smoke HTTP server failed: " + fault.Message, fault);
        }
    }

    private async Task ListenAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(_stop.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (HttpListenerException)
            {
                return;
            }

            _ = Task.Run(() => HandleAsync(context, _stop.Token), _stop.Token);
        }
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            byte[] rawBody = await ReadBodyAsync(context.Request, cancellationToken).ConfigureAwait(false);
            var snapshot = new HttpRequestSnapshot(
                new HttpMethod(context.Request.HttpMethod),
                context.Request.RawUrl ?? string.Empty,
                ReadBearerToken(context.Request),
                Encoding.UTF8.GetString(rawBody),
                rawBody);
            lock (_gate)
            {
                _requests.Add(snapshot);
            }

            await WriteResponseAsync(context.Response, snapshot, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsClientDisconnect(exception))
        {
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _faults.Enqueue(exception);
            if (context.Response.OutputStream.CanWrite)
            {
                await WriteTextAsync(context.Response, HttpStatusCode.InternalServerError, exception.Message, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            context.Response.Close();
        }
    }

    private async Task WriteResponseAsync(
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

    private static async Task<byte[]> ReadBodyAsync(HttpListenerRequest request, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        await request.InputStream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
        return memory.ToArray();
    }

    private static string? ReadBearerToken(HttpListenerRequest request)
    {
        string? authorization = request.Headers["Authorization"];
        const string prefix = "Bearer ";
        return authorization is not null && authorization.StartsWith(prefix, StringComparison.Ordinal)
            ? authorization[prefix.Length..]
            : null;
    }

    private static async Task WriteJsonAsync(
        HttpListenerResponse response,
        HttpStatusCode statusCode,
        object payload,
        CancellationToken cancellationToken)
    {
        byte[] body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions));
        response.StatusCode = (int)statusCode;
        response.ContentType = "application/json";
        response.ContentLength64 = body.Length;
        await response.OutputStream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteTextAsync(
        HttpListenerResponse response,
        HttpStatusCode statusCode,
        string bodyText,
        CancellationToken cancellationToken)
    {
        byte[] body = Encoding.UTF8.GetBytes(bodyText);
        response.StatusCode = (int)statusCode;
        response.ContentType = "text/plain";
        response.ContentLength64 = body.Length;
        await response.OutputStream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static bool IsClientDisconnect(Exception exception)
    {
        return exception is IOException or ObjectDisposedException or HttpListenerException;
    }
}
