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
    /// <summary>Normalized provider user-info claims.</summary>
    public record OidcUserInfoClaims(
        string? Subject,
        string? Email,
        bool? EmailVerified,
        string? Name,
        string? GivenName,
        string? FamilyName,
        string? Picture,
        string? PreferredUsername);
}
