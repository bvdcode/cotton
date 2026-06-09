// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Cotton.Sync.Cli.Tests.TestSupport
{
    internal abstract class SyncProcessCrashHttpServerBase : IAsyncDisposable
    {
        protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly CancellationTokenSource _stop = new();
        private readonly ConcurrentQueue<Exception> _faults = new();
        private readonly string _faultMessagePrefix;
        private readonly HttpListener _listener = new();
        private readonly List<HttpRequestSnapshot> _requests = [];
        private readonly object _gate = new();
        private Task? _listenTask;

        protected SyncProcessCrashHttpServerBase(string faultMessagePrefix)
        {
            _faultMessagePrefix = faultMessagePrefix;
            BaseUri = new Uri("http://127.0.0.1:" + GetFreePort().ToStringInvariant() + "/");
            _listener.Prefixes.Add(BaseUri.AbsoluteUri);
        }

        public Uri BaseUri { get; }

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
            ReleaseBlockedResponses();
            _stop.Cancel();
            _listener.Close();
            try
            {
                if (_listenTask is not null)
                {
                    await _listenTask.ConfigureAwait(false);
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (HttpListenerException)
            {
            }

            _stop.Dispose();
        }

        public void AssertNoFaults()
        {
            if (_faults.TryPeek(out Exception? fault))
            {
                throw new AssertionException(_faultMessagePrefix + ": " + fault.Message, fault);
            }
        }

        protected void Start()
        {
            _listener.Start();
            _listenTask = ListenAsync();
        }

        protected virtual void ReleaseBlockedResponses()
        {
        }

        protected abstract Task WriteResponseAsync(
            HttpListenerResponse response,
            HttpRequestSnapshot request,
            CancellationToken cancellationToken);

        protected static async Task WriteJsonAsync(
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

        protected static async Task WriteTextAsync(
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
                    rawBody)
                {
                    Headers = ReadHeaders(context.Request),
                };
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

        private static IReadOnlyDictionary<string, string> ReadHeaders(HttpListenerRequest request)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string? key in request.Headers.AllKeys)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    headers[key] = request.Headers[key] ?? string.Empty;
                }
            }

            return headers;
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
}
