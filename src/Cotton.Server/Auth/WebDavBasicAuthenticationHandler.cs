// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using EasyExtensions.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

namespace Cotton.Server.Auth;

public sealed class WebDavBasicAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    CottonDbContext dbContext,
    IPasswordHashService hasher,
    IMemoryCache cache)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string PolicyName = "WebDav";
    public const string SchemeName = "WebDavBasic";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(1);

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        (string username, string token)? creds;
        try
        {
            creds = ParseBasicCredentials(authHeader);
        }
        catch
        {
            return AuthenticateResult.Fail("Invalid Authorization header.");
        }

        if (creds is null || string.IsNullOrWhiteSpace(creds.Value.username) || string.IsNullOrWhiteSpace(creds.Value.token))
        {
            return AuthenticateResult.Fail("Invalid credentials.");
        }

        var username = creds.Value.username.Trim();
        var token = creds.Value.token;

        var cacheKey = $"webdav-basic:{username}:{token}";
        if (cache.TryGetValue(cacheKey, out Guid cachedUserId) && cachedUserId != Guid.Empty)
        {
            var principal = CreatePrincipal(cachedUserId, username);
            return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
        }

        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Username == username);
        if (user is null)
        {
            return AuthenticateResult.Fail("Invalid username or token.");
        }

        if (string.IsNullOrWhiteSpace(user.WebDavTokenPhc) || !hasher.Verify(token, user.WebDavTokenPhc))
        {
            return AuthenticateResult.Fail("Invalid username or token.");
        }

        cache.Set(cacheKey, user.Id, CacheTtl);

        var okPrincipal = CreatePrincipal(user.Id, user.Username);
        return AuthenticateResult.Success(new AuthenticationTicket(okPrincipal, Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Basic realm=\"Cotton WebDAV\", charset=\"UTF-8\"";
        return base.HandleChallengeAsync(properties);
    }

    private static ClaimsPrincipal CreatePrincipal(Guid userId, string username)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.Name, username),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        return new ClaimsPrincipal(identity);
    }

    private static (string username, string token)? ParseBasicCredentials(string authorizationHeader)
    {
        var encoded = authorizationHeader[(authorizationHeader.IndexOf(' ') + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return null;
        }

        var bytes = Convert.FromBase64String(encoded);
        var decoded = Encoding.UTF8.GetString(bytes);
        var idx = decoded.IndexOf(':');
        if (idx <= 0 || idx == decoded.Length - 1)
        {
            return null;
        }

        var username = decoded[..idx];
        var token = decoded[(idx + 1)..];
        return (username, token);
    }
}
