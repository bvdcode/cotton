// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Sdk
{
    /// <summary>
    /// Indicates that token renewal failed. The original failure is available as the inner exception.
    /// </summary>
    public class CottonTokenRefreshException(Exception innerException)
        : HttpRequestException(
            "Token renewal could not be completed.",
            innerException,
            (innerException as HttpRequestException)?.StatusCode)
    {
    }
}
