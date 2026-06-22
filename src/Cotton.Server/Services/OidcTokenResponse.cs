// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using EasyExtensions.AspNetCore.Exceptions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cotton.Server.Services
{
    /// <summary>
    /// OIDC token endpoint response fields used by Cotton.
    /// </summary>
    public class OidcTokenResponse
    {
        /// <summary>
        /// Provider ID token.
        /// </summary>
        [JsonPropertyName("id_token")]
        public string IdToken { get; set; } = string.Empty;

        /// <summary>
        /// Provider access token for user-info.
        /// </summary>
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
    }
}
