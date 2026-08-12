// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;

namespace Cotton.Previews.Http
{
    public class RangeStreamServer : IAsyncDisposable
    {
        private readonly record struct ByteRange(long Start, long EndInclusive)
        {
            public long ContentLength => (EndInclusive - Start) + 1;
        }

        private readonly HttpListener _listener;
        private readonly Stream _stream;
        private readonly long _length;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loop;
        private readonly string _token;
        private readonly SemaphoreSlim _sem = new(1, 1);
        private readonly ILogger? _logger;
        private readonly string _serverId;
        private readonly TimeSpan _requestDrainTimeout;
        private readonly object _activeHandlersLock = new();
        private readonly TaskCompletionSource _activeHandlersDrained = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _activeHandlers;
        private bool _disposing;

        public Uri Url { get; }

        public RangeStreamServer(
            Stream seekableStream,
            ILogger? logger = null,
            TimeSpan? requestDrainTimeout = null)
        {
            if (!seekableStream.CanSeek)
            {
                throw new ArgumentException("Stream must be seekable", nameof(seekableStream));
            }

            _logger = logger;
            _serverId = Guid.NewGuid().ToString("N")[..8];
            _requestDrainTimeout = requestDrainTimeout ?? TimeSpan.FromSeconds(5);
            _token = Guid.NewGuid().ToString("N");
            _stream = seekableStream;
            _length = seekableStream.Length;

            int port = GetFreeTcpPort();
            string prefix = $"http://127.0.0.1:{port}/";
            Url = new Uri(prefix + "video" + "?token=" + _token);

            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _listener.Start();

            _logger?.LogDebug("[RangeServer {ServerId}] Started on {Url}, stream length={Length}", _serverId, Url, _length);

            _loop = Task.Run(() => LoopAsync(_cts.Token));
        }

        private static int GetFreeTcpPort()
        {
            var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        private async Task LoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "[RangeServer {ServerId}] GetContextAsync exception", _serverId);
                    continue;
                }

                StartHandler(ctx, ct);
            }

            _logger?.LogDebug("[RangeServer {ServerId}] Loop ended", _serverId);
        }

        private void StartHandler(HttpListenerContext ctx, CancellationToken ct)
        {
            if (!TryBeginHandler())
            {
                TryAbortResponse(ctx, "rejecting request during shutdown");
                return;
            }

            try
            {
                _ = Task.Run(
                    async () =>
                    {
                        try
                        {
                            await HandleAsync(ctx, ct).ConfigureAwait(false);
                        }
                        finally
                        {
                            EndHandler();
                        }
                    },
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                EndHandler();
                _logger?.LogError(ex, "[RangeServer {ServerId}] Failed to start request handler", _serverId);
                TryAbortResponse(ctx, "starting request handler");
            }
        }

        private bool TryBeginHandler()
        {
            lock (_activeHandlersLock)
            {
                if (_disposing)
                {
                    return false;
                }

                _activeHandlers++;
                return true;
            }
        }

        private void EndHandler()
        {
            lock (_activeHandlersLock)
            {
                _activeHandlers--;
                if (_disposing && _activeHandlers == 0)
                {
                    _activeHandlersDrained.TrySetResult();
                }
            }
        }

        private void MarkDisposing()
        {
            lock (_activeHandlersLock)
            {
                _disposing = true;
                if (_activeHandlers == 0)
                {
                    _activeHandlersDrained.TrySetResult();
                }
            }
        }

        private Task WaitForActiveHandlersAsync()
        {
            lock (_activeHandlersLock)
            {
                return _activeHandlers == 0 ? Task.CompletedTask : _activeHandlersDrained.Task;
            }
        }

        private async Task<bool> TryWaitForActiveHandlersAsync()
        {
            Task drained = WaitForActiveHandlersAsync();
            if (drained.IsCompletedSuccessfully)
            {
                return true;
            }

            Task timeout = Task.Delay(_requestDrainTimeout);
            Task completed = await Task.WhenAny(drained, timeout).ConfigureAwait(false);
            return ReferenceEquals(completed, drained);
        }

        private async Task HandleAsync(HttpListenerContext ctx, CancellationToken ct)
        {
            var reqId = Guid.NewGuid().ToString("N")[..6];
            try
            {
                if (!TryAuthorize(ctx, reqId))
                {
                    return;
                }

                ConfigureResponseBase(ctx);

                var rangeHeader = ctx.Request.Headers["Range"];
                if (!TryParseRange(rangeHeader, out ByteRange? range, out var statusCode, out var contentRangeHeaderValue))
                {
                    ctx.Response.StatusCode = statusCode;
                    if (!string.IsNullOrEmpty(contentRangeHeaderValue))
                    {
                        ctx.Response.Headers["Content-Range"] = contentRangeHeaderValue;
                    }
                    ctx.Response.Close();
                    return;
                }

                if (range is null)
                {
                    await ServeFullAsync(ctx, reqId, ct).ConfigureAwait(false);
                    return;
                }

                await ServeRangeAsync(ctx, reqId, range.Value, ct).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException)
            {
                _logger?.LogDebug("[RangeServer {ServerId} Req {ReqId}] Range request cancelled by client", _serverId, reqId);
                TryAbortResponse(ctx, "cancelling range request");
            }
            catch (HttpListenerException ex) when (IsDisposing() || IsExpectedClientDisconnect(ex))
            {
                _logger?.LogDebug("[RangeServer {ServerId} Req {ReqId}] Client closed connection early (normal for ffmpeg)", _serverId, reqId);
                TryAbortResponse(ctx, "handling early client disconnect");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[RangeServer {ServerId} Req {ReqId}] HandleAsync exception", _serverId, reqId);
                TryAbortResponse(ctx, "handling request exception");
            }
        }

        private void TryAbortResponse(HttpListenerContext ctx, string reason)
        {
            try
            {
                ctx.Response.Abort();
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "[RangeServer {ServerId}] Response abort failed while {Reason}", _serverId, reason);
            }
        }

        internal static bool IsExpectedClientDisconnect(HttpListenerException exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            return exception.NativeErrorCode is 64 or 995 or 10053 or 10054 or 10058
                || exception.Message.Contains("reset by peer", StringComparison.OrdinalIgnoreCase)
                || exception.Message.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase)
                || exception.Message.Contains("broken pipe", StringComparison.OrdinalIgnoreCase)
                || exception.Message.Contains("cannot access a disposed object", StringComparison.OrdinalIgnoreCase)
                || exception.Message.Contains("SafeSocketHandle", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsDisposing()
        {
            lock (_activeHandlersLock)
            {
                return _disposing;
            }
        }

        private bool TryAuthorize(HttpListenerContext ctx, string reqId)
        {
            if (!string.Equals(ctx.Request.Url?.AbsolutePath, Url.AbsolutePath, StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogDebug("[RangeServer {ServerId} Req {ReqId}] Invalid path", _serverId, reqId);
                ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
                ctx.Response.Close();
                return false;
            }

            var token = ctx.Request.QueryString["token"];
            if (!string.Equals(token, _token, StringComparison.Ordinal))
            {
                _logger?.LogDebug("[RangeServer {ServerId} Req {ReqId}] Invalid token", _serverId, reqId);
                ctx.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                ctx.Response.Close();
                return false;
            }

            return true;
        }

        private static void ConfigureResponseBase(HttpListenerContext ctx)
        {
            ctx.Response.SendChunked = false;
            ctx.Response.KeepAlive = false;
            ctx.Response.Headers["Connection"] = "close";
            ctx.Response.Headers["Accept-Ranges"] = "bytes";
            ctx.Response.ContentType = "application/octet-stream";
        }

        private bool TryParseRange(
            string? range,
            out ByteRange? parsedRange,
            out int errorStatusCode,
            out string? contentRangeHeaderValue)
        {
            parsedRange = null;
            errorStatusCode = (int)HttpStatusCode.OK;
            contentRangeHeaderValue = null;

            if (string.IsNullOrWhiteSpace(range))
            {
                return true;
            }

            if (!RangeHeaderValue.TryParse(range, out RangeHeaderValue? rangeHeader)
                || !string.Equals(rangeHeader.Unit, "bytes", StringComparison.OrdinalIgnoreCase)
                || rangeHeader.Ranges.Count != 1)
            {
                errorStatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                return false;
            }

            RangeItemHeaderValue requestedRange = rangeHeader.Ranges.Single();
            if (!TryResolveRange(requestedRange, out long start, out long end))
            {
                errorStatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                return false;
            }

            if (start >= _length)
            {
                errorStatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                contentRangeHeaderValue = $"bytes */{_length}";
                return false;
            }

            end = Math.Clamp(end, start, _length - 1);
            parsedRange = new ByteRange(start, end);
            return true;
        }

        private bool TryResolveRange(RangeItemHeaderValue range, out long start, out long end)
        {
            start = 0;
            end = 0;

            if (range.From is null)
            {
                if (range.To is not long suffixLength || suffixLength <= 0)
                {
                    return false;
                }

                start = Math.Max(0, _length - suffixLength);
                end = _length - 1;
                return true;
            }

            start = range.From.Value;
            end = range.To ?? _length - 1;
            return true;
        }

        private async Task ServeFullAsync(HttpListenerContext ctx, string reqId, CancellationToken ct)
        {
            ctx.Response.StatusCode = (int)HttpStatusCode.OK;
            ctx.Response.ContentLength64 = _length;
            var ok = await CopyRangeAsync(reqId, start: 0, endInclusive: _length - 1, ctx.Response.OutputStream, ct).ConfigureAwait(false);
            if (!ok)
            {
                _logger?.LogWarning("[RangeServer {ServerId} Req {ReqId}] Full copy failed", _serverId, reqId);
                ctx.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
            ctx.Response.Close();
        }

        private async Task ServeRangeAsync(HttpListenerContext ctx, string reqId, ByteRange range, CancellationToken ct)
        {
            ctx.Response.StatusCode = (int)HttpStatusCode.PartialContent;
            ctx.Response.ContentLength64 = range.ContentLength;
            ctx.Response.Headers["Content-Range"] = $"bytes {range.Start}-{range.EndInclusive}/{_length}";

            var ok = await CopyRangeAsync(reqId, range.Start, range.EndInclusive, ctx.Response.OutputStream, ct).ConfigureAwait(false);
            if (!ok)
            {
                _logger?.LogWarning("[RangeServer {ServerId} Req {ReqId}] Range copy failed", _serverId, reqId);
                ctx.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
            ctx.Response.Close();
        }

        private async Task<bool> CopyRangeAsync(string reqId, long start, long endInclusive, Stream destination, CancellationToken ct)
        {
            long remaining = (endInclusive - start) + 1;
            long currentPosition = start;
            byte[] buffer = new byte[1024 * 1024];
            long totalRead = 0;

            _logger?.LogDebug("[RangeServer {ServerId} Req {ReqId}] CopyRangeAsync starting: {Start}-{End} ({ContentLength} bytes)", _serverId, reqId, start, endInclusive, remaining);

            while (remaining > 0)
            {
                int toRead = (int)Math.Min(buffer.Length, remaining);
                int read;

                await _sem.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    _stream.Seek(currentPosition, SeekOrigin.Begin);
                    read = await _stream.ReadAsync(buffer.AsMemory(0, toRead), ct).ConfigureAwait(false);
                }
                finally
                {
                    _sem.Release();
                }

                if (read <= 0)
                {
                    _logger?.LogError("[RangeServer {ServerId} Req {ReqId}] Premature EOF at position {Position}: totalRead={TotalRead}, expected={Expected}", _serverId, reqId, currentPosition, totalRead, (endInclusive - start) + 1);
                    return false;
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                remaining -= read;
                currentPosition += read;
                totalRead += read;
            }

            await destination.FlushAsync(ct).ConfigureAwait(false);
            _logger?.LogDebug("[RangeServer {ServerId} Req {ReqId}] CopyRangeAsync completed: totalRead={TotalRead}", _serverId, reqId, totalRead);
            return true;
        }

        public async ValueTask DisposeAsync()
        {
            _logger?.LogDebug("[RangeServer {ServerId}] Disposing...", _serverId);
            MarkDisposing();
            _cts.Cancel();
            try { _listener.Stop(); } catch { }
            try { _listener.Close(); } catch { }
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "[RangeServer {ServerId}] Loop shutdown failed", _serverId);
            }

            bool handlersDrained = await TryWaitForActiveHandlersAsync().ConfigureAwait(false);
            if (handlersDrained)
            {
                _cts.Dispose();
                _sem.Dispose();
            }
            else
            {
                _logger?.LogWarning(
                    "[RangeServer {ServerId}] Timed out waiting {Timeout} for active range handlers to stop.",
                    _serverId,
                    _requestDrainTimeout);
            }

            _logger?.LogDebug("[RangeServer {ServerId}] Disposed", _serverId);
        }
    }
}
