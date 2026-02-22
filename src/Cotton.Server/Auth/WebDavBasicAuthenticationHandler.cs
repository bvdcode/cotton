// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using EasyExtensions.Abstractions;
using EasyExtensions.AspNetCore.Extensions;
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
        if (TryGetBasicAuthHeaderFailure(authHeader, out var headerFailure))
        {
            return headerFailure;
        }

        if (!TryParseAndValidateCredentials(authHeader, out var username, out var token, out var credentialsFailure))
        {
            return credentialsFailure;
        }

        var cacheKey = authCache.GetCacheKey(username, token);
        if (TryAuthenticateFromCache(cacheKey, username, out var cachedResult))
        {
            return cachedResult;
        }

        Logger.LogDebug("WebDAV auth: cache miss for username '{Username}'.", username);

        var user = await dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Username == username || x.Email == username);
        if (user is null)
        {
            Logger.LogInformation("WebDAV auth: user '{Username}' not found.", username);
            return AuthenticateResult.Fail("Invalid username or token.");
        }

        var tokenResult = await VerifyTokenOrFailAsync(user, username, token);
        if (tokenResult is not null)
        {
            return tokenResult;
        }

        cache.Set(cacheKey, user.Id, CacheTtl);

        Logger.LogDebug("WebDAV auth: authentication successful for user '{Username}' ({UserId}).", user.Username, user.Id);

        return AuthenticateSuccess(user.Id, user.Username);
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
        string? decoded = Encoding.UTF8.GetString(bytes).Split('\n').FirstOrDefault(x => x.Contains(':'));
        if (decoded == null)
        {
            return null;
        }
        var idx = decoded.IndexOf(':');
        if (idx <= 0)
        {
            return null;
        }

        var username = decoded[..idx];
        var token = decoded[(idx + 1)..];
        return (username, token);
    }

    private bool TryGetBasicAuthHeaderFailure(string authHeader, out AuthenticateResult failure)
    {
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogInformation("WebDAV auth: missing or non-Basic Authorization header.");
            failure = AuthenticateResult.NoResult();
            return true;
        }

        failure = default!;
        return false;
    }

    private bool TryParseAndValidateCredentials(
        string authHeader,
        out string username,
        out string token,
        out AuthenticateResult failure)
    {
        username = string.Empty;
        token = string.Empty;

        (string username, string token)? creds;
        try
        {
            creds = ParseBasicCredentials(authHeader);
        }
        catch
        {
            Logger.LogInformation("WebDAV auth: invalid Basic Authorization header (base64 decode failed).");
            failure = AuthenticateResult.Fail("Invalid Authorization header.");
            return false;
        }

        if (creds is null || string.IsNullOrWhiteSpace(creds.Value.username) || string.IsNullOrWhiteSpace(creds.Value.token))
        {
            Logger.LogWarning(
                "WebDAV auth: invalid Basic credentials (username empty: {UsernameEmpty}, token empty: {TokenEmpty}).",
                creds is null || string.IsNullOrWhiteSpace(creds.Value.username),
                creds is null || string.IsNullOrWhiteSpace(creds.Value.token));
            failure = AuthenticateResult.Fail("Invalid credentials.");
            return false;
        }

        username = creds.Value.username.Trim();
        token = creds.Value.token;

        if (string.IsNullOrWhiteSpace(username))
        {
            Logger.LogInformation("WebDAV auth: username is whitespace after trimming.");
            failure = AuthenticateResult.Fail("Invalid credentials.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            Logger.LogInformation("WebDAV auth: empty token provided for username '{Username}'.", username);
            failure = AuthenticateResult.Fail("Invalid credentials.");
            return false;
        }

        failure = default!;
        return true;
    }

    private bool TryAuthenticateFromCache(string cacheKey, string username, out AuthenticateResult result)
    {
        if (cache.TryGetValue(cacheKey, out Guid cachedUserId) && cachedUserId != Guid.Empty)
        {
            Logger.LogInformation("WebDAV auth: cache hit for username '{Username}'.", username);
            result = AuthenticateSuccess(cachedUserId, username);
            return true;
        }

        result = default!;
        return false;
    }

    private async Task<AuthenticateResult?> VerifyTokenOrFailAsync(User user, string username, string token)
    {
        if (string.IsNullOrWhiteSpace(user.WebDavTokenPhc))
        {
            Logger.LogWarning(
                "WebDAV auth: stored WebDAV token hash is missing for user '{Username}' ({UserId}).",
                user.Username,
                user.Id);
            return AuthenticateResult.Fail("Invalid username or token.");
        }

        if (hasher.Verify(token, user.WebDavTokenPhc))
        {
            return null;
        }

        Logger.LogWarning(
            "WebDAV auth: invalid token for user '{Username}' ({UserId}). Remote IP: {RemoteIp}",
            user.Username,
            user.Id,
            Request.GetRemoteIPAddress());

        try
        {
            await notifications.SendFailedLoginAttemptAsync(
                user.Id,
                username,
                Request.GetRemoteIPAddress(),
                Request.Headers.UserAgent);
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "WebDAV auth: failed to send failed-login notification for user '{Username}' ({UserId}).",
                user.Username,
                user.Id);
        }

        return AuthenticateResult.Fail("Invalid username or token.");
    }

    private AuthenticateResult AuthenticateSuccess(Guid userId, string username)
    {
        var principal = CreatePrincipal(userId, username);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
