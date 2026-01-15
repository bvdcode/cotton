using Microsoft.Extensions.Logging;
using System.Net;

namespace Cotton.Previews.Http
{
    internal sealed class RangeStreamServer : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly Stream _stream;
        private readonly long _length;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loop;
        private readonly string _token;
        private readonly SemaphoreSlim _sem = new(1, 1);
        private readonly ILogger? _logger;
        private readonly string _serverId;

        public Uri Url { get; }

        public RangeStreamServer(Stream seekableStream, ILogger? logger = null)
        {
            if (!seekableStream.CanSeek)
            {
                throw new ArgumentException("Stream must be seekable", nameof(seekableStream));
            }

            _logger = logger;
            _serverId = Guid.NewGuid().ToString("N")[..8];
            _token = Guid.NewGuid().ToString("N");
            _stream = seekableStream;
            _length = seekableStream.Length;

            int port = GetFreeTcpPort();
            string prefix = $"http://127.0.0.1:{port}/";
            Url = new Uri(prefix + "video" + "?token=" + _token);

            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _listener.Start();

            _logger?.LogInformation("[RangeServer {ServerId}] Started on {Url}, stream length={Length}", _serverId, Url, _length);

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

                _ = Task.Run(() => HandleAsync(ctx, ct), ct);
            }

            _logger?.LogInformation("[RangeServer {ServerId}] Loop ended", _serverId);
        }

        private async Task HandleAsync(HttpListenerContext ctx, CancellationToken ct)
        {
            var reqId = Guid.NewGuid().ToString("N")[..6];
            try
            {
                if (!string.Equals(ctx.Request.Url?.AbsolutePath, Url.AbsolutePath, StringComparison.OrdinalIgnoreCase))
                {
                    _logger?.LogWarning("[RangeServer {ServerId} Req {ReqId}] Invalid path: {Path}", _serverId, reqId, ctx.Request.Url?.AbsolutePath);
                    ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    ctx.Response.Close();
                    return;
                }

                var token = ctx.Request.QueryString["token"];
                if (!string.Equals(token, _token, StringComparison.Ordinal))
                {
                    _logger?.LogWarning("[RangeServer {ServerId} Req {ReqId}] Invalid token", _serverId, reqId);
                    ctx.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    ctx.Response.Close();
                    return;
                }

                ctx.Response.SendChunked = false;
                ctx.Response.KeepAlive = false;
                ctx.Response.Headers["Connection"] = "close";
                ctx.Response.Headers["Accept-Ranges"] = "bytes";
                ctx.Response.ContentType = "application/octet-stream";

                string? range = ctx.Request.Headers["Range"];
                if (string.IsNullOrWhiteSpace(range))
                {
                    _logger?.LogInformation("[RangeServer {ServerId} Req {ReqId}] Full file request, length={Length}", _serverId, reqId, _length);
                    ctx.Response.StatusCode = (int)HttpStatusCode.OK;
                    ctx.Response.ContentLength64 = _length;
                    bool ok = await CopyRangeAsync(reqId, start: 0, endInclusive: _length - 1, ctx.Response.OutputStream, ct).ConfigureAwait(false);
                    if (!ok)
                    {
                        _logger?.LogError("[RangeServer {ServerId} Req {ReqId}] Full file copy failed", _serverId, reqId);
                        ctx.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    }
                    else
                    {
                        _logger?.LogInformation("[RangeServer {ServerId} Req {ReqId}] Full file copy succeeded", _serverId, reqId);
                    }
                    ctx.Response.Close();
                    return;
                }

                if (!range.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
                {
                    _logger?.LogWarning("[RangeServer {ServerId} Req {ReqId}] Invalid range header: {Range}", _serverId, reqId, range);
                    ctx.Response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                    ctx.Response.Close();
                    return;
                }

                var value = range[6..];
                var parts = value.Split('-', 2);
                long start;
                long end;

                if (parts.Length == 2 && parts[0].Length == 0)
                {
                    if (!long.TryParse(parts[1], out var suffixLen) || suffixLen <= 0)
                    {
                        _logger?.LogWarning("[RangeServer {ServerId} Req {ReqId}] Invalid suffix range: {Range}", _serverId, reqId, range);
                        ctx.Response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                        ctx.Response.Close();
                        return;
                    }

                    start = Math.Max(0, _length - suffixLen);
                    end = _length - 1;
                }
                else
                {
                    if (!long.TryParse(parts[0], out start) || start < 0)
                    {
                        _logger?.LogWarning("[RangeServer {ServerId} Req {ReqId}] Invalid start in range: {Range}", _serverId, reqId, range);
                        ctx.Response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                        ctx.Response.Close();
                        return;
                    }

                    if (parts.Length == 2 && parts[1].Length > 0)
                    {
                        if (!long.TryParse(parts[1], out end) || end < start)
                        {
                            _logger?.LogWarning("[RangeServer {ServerId} Req {ReqId}] Invalid end in range: {Range}", _serverId, reqId, range);
                            ctx.Response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                            ctx.Response.Close();
                            return;
                        }
                    }
                    else
                    {
                        end = _length - 1;
                    }
                }

                if (start >= _length)
                {
                    _logger?.LogWarning("[RangeServer {ServerId} Req {ReqId}] Start beyond length: start={Start}, length={Length}", _serverId, reqId, start, _length);
                    ctx.Response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                    ctx.Response.Headers["Content-Range"] = $"bytes */{_length}";
                    ctx.Response.Close();
                    return;
                }

                end = Math.Clamp(end, start, _length - 1);
                long contentLength = (end - start) + 1;

                _logger?.LogInformation("[RangeServer {ServerId} Req {ReqId}] Range request: {Start}-{End} (contentLength={ContentLength})", _serverId, reqId, start, end, contentLength);

                ctx.Response.StatusCode = (int)HttpStatusCode.PartialContent;
                ctx.Response.ContentLength64 = contentLength;
                ctx.Response.Headers["Content-Range"] = $"bytes {start}-{end}/{_length}";

                bool okRange = await CopyRangeAsync(reqId, start, end, ctx.Response.OutputStream, ct).ConfigureAwait(false);
                if (!okRange)
                {
                    _logger?.LogError("[RangeServer {ServerId} Req {ReqId}] Range copy failed", _serverId, reqId);
                    ctx.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                }
                else
                {
                    _logger?.LogInformation("[RangeServer {ServerId} Req {ReqId}] Range copy succeeded", _serverId, reqId);
                }
                ctx.Response.Close();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[RangeServer {ServerId} Req {ReqId}] HandleAsync exception", _serverId, reqId);
                try { ctx.Response.Abort(); } catch { }
            }
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

                // Lock only Seek+Read to allow interleaving of multiple concurrent requests
                // This is critical: ffprobe/ffmpeg request full file AND moov simultaneously
                // Without interleaving, moov request waits for 500MB+ transfer ? timeout
                await _sem.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    _stream.Seek(currentPosition, SeekOrigin.Begin);
                    read = _stream.Read(buffer, 0, toRead);
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
            _logger?.LogInformation("[RangeServer {ServerId}] Disposing...", _serverId);
            _cts.Cancel();
            try { _listener.Stop(); } catch { }
            try { _listener.Close(); } catch { }
            try { await _loop.ConfigureAwait(false); } catch { }
            _cts.Dispose();
            _sem.Dispose();
            _logger?.LogInformation("[RangeServer {ServerId}] Disposed", _serverId);
        }
    }
}
