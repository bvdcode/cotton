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

        public Uri Url { get; }

        public RangeStreamServer(Stream seekableStream)
        {
            if (!seekableStream.CanSeek)
            {
                throw new ArgumentException("Stream must be seekable", nameof(seekableStream));
            }

            string token = Guid.NewGuid().ToString("N");
            _stream = seekableStream;
            _length = seekableStream.Length;

            int port = GetFreeTcpPort();
            string prefix = $"http://127.0.0.1:{port}/";
            Url = new Uri(prefix + "video" + "?token=" + token);

            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _listener.Start();

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
                catch
                {
                    continue;
                }

                _ = Task.Run(() => HandleAsync(ctx, ct), ct);
            }
        }

        private async Task HandleAsync(HttpListenerContext ctx, CancellationToken ct)
        {
            try
            {
                if (!string.Equals(ctx.Request.Url?.AbsolutePath, Url.AbsolutePath, StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    ctx.Response.Close();
                    return;
                }

                ctx.Response.SendChunked = false;
                ctx.Response.Headers["Accept-Ranges"] = "bytes";

                string? range = ctx.Request.Headers["Range"];
                if (string.IsNullOrWhiteSpace(range))
                {
                    ctx.Response.StatusCode = (int)HttpStatusCode.OK;
                    ctx.Response.ContentType = "application/octet-stream";
                    ctx.Response.ContentLength64 = _length;
                    await CopyRangeAsync(start: 0, endInclusive: _length - 1, ctx.Response.OutputStream, ct).ConfigureAwait(false);
                    ctx.Response.OutputStream.Close();
                    return;
                }

                // Range: bytes=start-end
                if (!range.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
                {
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
                    // suffix range: bytes=-N
                    if (!long.TryParse(parts[1], out var suffixLen) || suffixLen <= 0)
                    {
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
                        ctx.Response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                        ctx.Response.Close();
                        return;
                    }

                    if (parts.Length == 2 && parts[1].Length > 0)
                    {
                        if (!long.TryParse(parts[1], out end) || end < start)
                        {
                            ctx.Response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                            ctx.Response.Close();
                            return;
                        }
                    }
                    else
                    {
                        end = _length - 1;
                    }

                    end = Math.Min(end, _length - 1);
                }

                ctx.Response.StatusCode = (int)HttpStatusCode.PartialContent;
                ctx.Response.ContentType = "application/octet-stream";
                ctx.Response.ContentLength64 = (end - start) + 1;
                ctx.Response.Headers["Content-Range"] = $"bytes {start}-{end}/{_length}";

                await CopyRangeAsync(start, end, ctx.Response.OutputStream, ct).ConfigureAwait(false);
                ctx.Response.OutputStream.Close();
            }
            catch
            {
                try { ctx.Response.Abort(); } catch { }
            }
        }

        private async Task CopyRangeAsync(long start, long endInclusive, Stream destination, CancellationToken ct)
        {
            // Not thread-safe: guard the underlying stream to keep seeks consistent.
            // Serialized access is fine: ffmpeg will request ranges sequentially for probing.
            lock (_stream)
            {
                _stream.Seek(start, SeekOrigin.Begin);
            }

            long remaining = (endInclusive - start) + 1;
            byte[] buffer = new byte[1024 * 1024];

            while (remaining > 0)
            {
                int toRead = (int)Math.Min(buffer.Length, remaining);
                int read;
                lock (_stream)
                {
                    read = _stream.Read(buffer, 0, toRead);
                }

                if (read <= 0)
                {
                    break;
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                remaining -= read;
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            try { _listener.Stop(); } catch { }
            try { _listener.Close(); } catch { }
            try { await _loop.ConfigureAwait(false); } catch { }
            _cts.Dispose();
        }
    }
}
