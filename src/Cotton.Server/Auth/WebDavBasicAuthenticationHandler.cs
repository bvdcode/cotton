// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using EasyExtensions.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
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
    IMemoryCache cache,
    Cotton.Server.Services.WebDav.WebDavAuthCache authCache,
    INotificationsProvider notifications)
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
            Logger.LogInformation("WebDAV auth: missing or non-Basic Authorization header.");
            return AuthenticateResult.NoResult();
        }

        (string username, string token)? creds;
        try
        {
            creds = ParseBasicCredentials(authHeader);
        }
        catch
        {
            Logger.LogInformation("WebDAV auth: invalid Basic Authorization header (base64 decode failed).");
            return AuthenticateResult.Fail("Invalid Authorization header.");
        }

        if (creds is null || string.IsNullOrWhiteSpace(creds.Value.username) || string.IsNullOrWhiteSpace(creds.Value.token))
        {
            Logger.LogWarning(
                "WebDAV auth: invalid Basic credentials (username empty: {UsernameEmpty}, token empty: {TokenEmpty}).",
                creds is null || string.IsNullOrWhiteSpace(creds.Value.username),
                creds is null || string.IsNullOrWhiteSpace(creds.Value.token));
            return AuthenticateResult.Fail("Invalid credentials.");
        }

        var username = creds.Value.username.Trim();
        var token = creds.Value.token;

        if (string.IsNullOrWhiteSpace(username))
        {
            Logger.LogInformation("WebDAV auth: username is whitespace after trimming.");
            return AuthenticateResult.Fail("Invalid credentials.");
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            Logger.LogInformation("WebDAV auth: empty token provided for username '{Username}'.", username);
            return AuthenticateResult.Fail("Invalid credentials.");
        }

        var cacheKey = authCache.GetCacheKey(username, token);
        if (cache.TryGetValue(cacheKey, out Guid cachedUserId) && cachedUserId != Guid.Empty)
        {
            Logger.LogInformation("WebDAV auth: cache hit for username '{Username}'.", username);
            var principal = CreatePrincipal(cachedUserId, username);
            return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
        }

        Logger.LogDebug("WebDAV auth: cache miss for username '{Username}'.", username);

        var user = await dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Username == username || x.Email == username);
        if (user is null)
        {
            Logger.LogInformation("WebDAV auth: user '{Username}' not found.", username);
            return AuthenticateResult.Fail("Invalid username or token.");
        }

        if (string.IsNullOrWhiteSpace(user.WebDavTokenPhc))
        {
            Logger.LogWarning("WebDAV auth: stored WebDAV token hash is missing for user '{Username}' ({UserId}).", user.Username, user.Id);
            return AuthenticateResult.Fail("Invalid username or token.");
        }

        if (!hasher.Verify(token, user.WebDavTokenPhc))
        {
            Logger.LogWarning(
                "WebDAV auth: invalid token for user '{Username}' ({UserId}). Remote IP: {RemoteIp}",
                user.Username,
                user.Id,
                Request.HttpContext.Connection.RemoteIpAddress);

            try
            {
                await notifications.SendFailedLoginAttemptAsync(
                    user.Id,
                    username,
                    Request.HttpContext.Connection.RemoteIpAddress ?? System.Net.IPAddress.None,
                    Request.Headers.UserAgent);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "WebDAV auth: failed to send failed-login notification for user '{Username}' ({UserId}).", user.Username, user.Id);
            }

            return AuthenticateResult.Fail("Invalid username or token.");
        }

        cache.Set(cacheKey, user.Id, CacheTtl);

        Logger.LogDebug("WebDAV auth: authentication successful for user '{Username}' ({UserId}).", user.Username, user.Id);

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
        const string basicPrefix = "Basic ";
        var encoded = authorizationHeader[basicPrefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return null;
        }

        var bytes = Convert.FromBase64String(encoded);
        var decoded = Encoding.UTF8.GetString(bytes);
        var idx = decoded.IndexOf(':');
        if (idx <= 0)
        {
            return null;
        }

        var username = decoded[..idx];
        var token = decoded[(idx + 1)..];
        return (username, token);
    }
}
