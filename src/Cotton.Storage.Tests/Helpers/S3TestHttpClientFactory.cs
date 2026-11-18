// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Amazon.Runtime;

namespace Cotton.Storage.Tests.Helpers
{
    internal class S3TestHttpClientFactory(HttpClient client) : HttpClientFactory
    {
        public override HttpClient CreateHttpClient(IClientConfig clientConfig) => client;
    }
}
