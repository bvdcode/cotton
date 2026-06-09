// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net;
using System.Text;
using System.Text.Json;

namespace Cotton.Sdk.Tests.Fakes
{
    internal sealed class QueuedHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

        public List<HttpRequestMessageSnapshot> Requests { get; } = [];

        public void EnqueueJson(HttpStatusCode statusCode, object payload)
        {
            _responses.Enqueue(_ => new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonSerializerOptions.Web), Encoding.UTF8, "application/json"),
            });
        }

        public void Enqueue(HttpStatusCode statusCode, string body = "")
        {
            _responses.Enqueue(_ => new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "text/plain"),
            });
        }

        public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responses.Enqueue(responseFactory);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            byte[] rawBody = request.Content is null
                ? []
                : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            Requests.Add(new HttpRequestMessageSnapshot(
                request.Method,
                request.RequestUri?.PathAndQuery ?? string.Empty,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                request.Headers.ToDictionary(
                    header => header.Key,
                    header => string.Join(",", header.Value),
                    StringComparer.OrdinalIgnoreCase),
                request.Content?.Headers.ContentType?.MediaType,
                body,
                rawBody));

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No queued HTTP response is available.");
            }

            return _responses.Dequeue()(request);
        }
    }

    internal sealed record HttpRequestMessageSnapshot(
        HttpMethod Method,
        string PathAndQuery,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        IReadOnlyDictionary<string, string> Headers,
        string? ContentType,
        string Body,
        byte[] RawBody);
}
