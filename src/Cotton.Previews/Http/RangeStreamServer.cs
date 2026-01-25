using Microsoft.Extensions.Logging;
using System.Net;

namespace Cotton.Previews.Http
{
    internal sealed class RangeStreamServer : IAsyncDisposable
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
                if (!TryAuthorize(ctx, reqId))
                {
                    return;
                }

                ConfigureResponseBase(ctx);

                var rangeHeader = ctx.Request.Headers["Range"];
                if (!TryParseRange(rangeHeader, out var range, out var statusCode, out var contentRangeHeaderValue))
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
            catch (HttpListenerException ex) when (ex.Message.Contains("reset by peer", StringComparison.OrdinalIgnoreCase)
                                                  || ex.Message.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase)
                                                  || ex.Message.Contains("broken pipe", StringComparison.OrdinalIgnoreCase))
            {
                // Client (ffmpeg/ffprobe) closed connection early - this is normal behavior
                // when they got what they needed (moov atom) and don't need the rest of the file
                _logger?.LogDebug("[RangeServer {ServerId} Req {ReqId}] Client closed connection early (normal for ffmpeg)", _serverId, reqId);
                try { ctx.Response.Abort(); } catch { }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[RangeServer {ServerId} Req {ReqId}] HandleAsync exception", _serverId, reqId);
                try { ctx.Response.Abort(); } catch { }
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

            if (!range.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogDebug("[RangeServer {ServerId}] Invalid range header", _serverId);
                errorStatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                return false;
            }

            var value = range[6..];
            var parts = value.Split('-', 2);
            long start;
            long end;

            // suffix range: bytes=-N
            if (parts.Length == 2 && parts[0].Length == 0)
            {
                if (!long.TryParse(parts[1], out var suffixLen) || suffixLen <= 0)
                {
                    errorStatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                    return false;
                }

                start = Math.Max(0, _length - suffixLen);
                end = _length - 1;
            }
            else
            {
                if (!long.TryParse(parts[0], out start) || start < 0)
                {
                    errorStatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                    return false;
                }

                if (parts.Length == 2 && parts[1].Length > 0)
                {
                    if (!long.TryParse(parts[1], out end) || end < start)
                    {
                        errorStatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                        return false;
                    }
                }
                else
                {
                    end = _length - 1;
                }
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

                // Lock only Seek+Read to allow interleaving of multiple concurrent requests
                // This is critical: ffprobe/ffmpeg request full file AND moov simultaneously
                // Without interleaving, moov request waits for 500MB+ transfer, as result is timeout
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
