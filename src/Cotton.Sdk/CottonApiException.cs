// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net;

namespace Cotton.Sdk;

/// <summary>
/// Represents a non-success HTTP response returned by the Cotton API.
/// </summary>
public sealed class CottonApiException : HttpRequestException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CottonApiException" /> class.
    /// </summary>
    public CottonApiException(HttpStatusCode statusCode, string? responseBody, string message)
        : base(message, null, statusCode)
    {
        ResponseBody = responseBody;
    }

    /// <summary>
    /// Gets the response body returned by the server, if it was available.
    /// </summary>
    public string? ResponseBody { get; }
}
