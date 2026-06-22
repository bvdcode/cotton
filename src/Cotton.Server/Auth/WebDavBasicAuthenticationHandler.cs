// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions.Abstractions;
using EasyExtensions.AspNetCore.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

namespace Cotton.Server.Auth
{
    /// <summary>
    /// Represents web dav basic authentication handler.
    /// </summary>
    public class WebDavBasicAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        CottonDbContext dbContext,
        IPasswordHashService hasher,
        IMemoryCache cache,
        Cotton.Server.Services.WebDav.WebDavAuthCache authCache,
        INotificationsProvider notifications,
        IGeoLookupService geoLookup,
        IDatabaseIntegrityVerifier integrity)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        /// <summary>
        /// Defines the policy name.
        /// </summary>
        public const string PolicyName = "WebDav";
        /// <summary>
        /// Defines the scheme name.
        /// </summary>
        public const string SchemeName = "WebDavBasic";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan FailedAttemptWindow = TimeSpan.FromMinutes(1);
        private const int FailedAttemptLimit = 10;
        private const string RateLimitedContextItemKey = "__cotton_webdav_basic_rate_limited";

        private IPAddress GetRequestIpAddress()
        {
            return Constants.IsPublicInstance
                ? IPAddress.Loopback
                : Request.GetRemoteIPAddress();
        }

        /// <inheritdoc />
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

            var rateLimitResult = TryRejectRateLimitedCredentials(username);
            if (rateLimitResult is not null)
            {
                return rateLimitResult;
            }

            Logger.LogDebug("WebDAV auth: cache miss for username '{Username}'.", username);

            var user = await dbContext.Users
                .FirstOrDefaultAsync(x => x.Username == username || x.Email == username);
            if (user is null)
            {
                Logger.LogInformation("WebDAV auth: user '{Username}' not found.", username);
                RecordAuthenticationFailure(username);
                return AuthenticateResult.Fail("Invalid username or token.");
            }

            integrity.RequireValid(dbContext, user, "webdav.auth");

            var tokenResult = await VerifyTokenOrFailAsync(user, username, token);
            if (tokenResult is not null)
            {
                return tokenResult;
            }

            cache.Set(cacheKey, user.Id, CacheTtl);
            ClearAuthenticationFailures(username);

            Logger.LogDebug("WebDAV auth: authentication successful for user '{Username}' ({UserId}).", user.Username, user.Id);

            return AuthenticateSuccess(user.Id, user.Username);
        }

        /// <inheritdoc />
        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            if (Context.Items.ContainsKey(RateLimitedContextItemKey))
            {
                Response.StatusCode = StatusCodes.Status429TooManyRequests;
                return Task.CompletedTask;
            }

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
                Logger.LogDebug("WebDAV auth: cache hit for username '{Username}'.", username);
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
                RecordAuthenticationFailure(username);
                return AuthenticateResult.Fail("Invalid username or token.");
            }

            if (hasher.Verify(token, user.WebDavTokenPhc))
            {
                return null;
            }

            RecordAuthenticationFailure(username);

            Logger.LogWarning(
                "WebDAV auth: invalid token for user '{Username}' ({UserId}). Remote IP: {RemoteIp}",
                user.Username,
                user.Id,
                GetRequestIpAddress());

            try
            {
                await notifications.SendFailedLoginAttemptAsync(
                    geoLookup,
                    user.Id,
                    username,
                    GetRequestIpAddress(),
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

        private AuthenticateResult? TryRejectRateLimitedCredentials(string username)
        {
            var counter = GetFailureCounter(username);
            if (counter is null || counter.Count < FailedAttemptLimit)
            {
                return null;
            }

            Context.Items[RateLimitedContextItemKey] = true;
            Logger.LogWarning(
                "WebDAV auth: rate limited username '{Username}' from remote IP {RemoteIp}.",
                username,
                Request.GetRemoteIPAddress());
            return AuthenticateResult.Fail("Too many WebDAV authentication attempts.");
        }

        private FailureCounter? GetFailureCounter(string username)
        {
            string key = GetFailureCacheKey(username);
            if (!cache.TryGetValue(key, out FailureCounter? counter))
            {
                return null;
            }

            if (counter!.ResetAt > DateTimeOffset.UtcNow)
            {
                return counter;
            }

            cache.Remove(key);
            return null;
        }

        private void RecordAuthenticationFailure(string username)
        {
            string key = GetFailureCacheKey(username);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            FailureCounter counter = GetFailureCounter(username) ?? new FailureCounter(0, now.Add(FailedAttemptWindow));
            counter.Count++;
            cache.Set(key, counter, counter.ResetAt);
        }

        private void ClearAuthenticationFailures(string username)
        {
            cache.Remove(GetFailureCacheKey(username));
        }

        private string GetFailureCacheKey(string username)
        {
            string normalizedUsername = username.Trim().ToLowerInvariant();
            return $"webdav-basic-fail:{Request.GetRemoteIPAddress()}:{normalizedUsername}";
        }

        private AuthenticateResult AuthenticateSuccess(Guid userId, string username)
        {
            var principal = CreatePrincipal(userId, username);
            return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
        }

        private class FailureCounter(int count, DateTimeOffset resetAt)
        {
            public int Count { get; set; } = count;
            public DateTimeOffset ResetAt { get; } = resetAt;
        }
    }
}
