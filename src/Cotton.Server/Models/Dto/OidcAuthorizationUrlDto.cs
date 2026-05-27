// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto;

/// <summary>OIDC authorization URL returned to the browser.</summary>
public sealed class OidcAuthorizationUrlDto
{
    /// <summary>Provider authorization URL.</summary>
    public string AuthorizationUrl { get; set; } = string.Empty;
}
