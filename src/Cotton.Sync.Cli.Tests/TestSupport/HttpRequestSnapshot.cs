// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Cli.Tests.TestSupport;

internal sealed record HttpRequestSnapshot(
    HttpMethod Method,
    string PathAndQuery,
    string? AuthorizationParameter,
    string Body,
    byte[] RawBody)
{
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public string? GetHeader(string name)
    {
        return Headers.TryGetValue(name, out string? value) ? value : null;
    }
}
