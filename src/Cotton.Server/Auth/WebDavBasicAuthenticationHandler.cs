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
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

namespace Cotton.Server.Auth
{
    public class WebDavBasicAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        CottonDbContext dbContext,
        IPasswordHashService hasher,
        IMemoryCache cache,
        Cotton.Server.Services.WebDav.WebDavAuthCache authCache,
        WebDavAuthenticationFailureLimiter authenticationFailureLimiter,
        INotificationsProvider notifications,
        IGeoLookupService geoLookup,
        IDatabaseIntegrityVerifier integrity)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string PolicyName = "WebDav";

        public const string SchemeName = "WebDavBasic";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(1);
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);
        private const string RateLimitedContextItemKey = "__cotton_webdav_basic_rate_limited";

        private IPAddress GetRequestIpAddress()
        {
            return Constants.IsPublicInstance
                ? IPAddress.Loopback
                : Request.GetTrustedClientIPAddress();
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string authHeader = Request.Headers.Authorization.ToString();
            if (!TryGetBasicAuthParameter(authHeader, out string encodedCredentials, out AuthenticateResult headerFailure))
            {
                return headerFailure;
            }

            if (!TryParseAndValidateCredentials(
                encodedCredentials,
                out string username,
                out string token,
                out AuthenticateResult credentialsFailure))
            {
                return credentialsFailure;
            }

            string cacheKey = authCache.GetCacheKey(username, token);
            if (TryAuthenticateFromCache(cacheKey, username, out AuthenticateResult? cachedResult))
            {
                return cachedResult;
            }

            AuthenticateResult? rateLimitResult = TryRejectRateLimitedCredentials(username);
            if (rateLimitResult is not null)
            {
                return rateLimitResult;
            }

            Logger.LogDebug("WebDAV auth: cache miss for username '{Username}'.", username);

            User? user = await dbContext.Users
                .FirstOrDefaultAsync(x => x.Username == username || x.Email == username);
            if (user is null)
            {
                Logger.LogInformation("WebDAV auth: user '{Username}' not found.", username);
                RecordAuthenticationFailure(username);
                return AuthenticateResult.Fail("Invalid username or token.");
            }

            integrity.RequireValid(dbContext, user, "webdav.auth");

            AuthenticateResult? tokenResult = await VerifyTokenOrFailAsync(user, username, token);
            if (tokenResult is not null)
            {
                return tokenResult;
            }

            cache.Set(cacheKey, user.Id, CacheTtl);
            ClearAuthenticationFailures(username);

            Logger.LogDebug("WebDAV auth: authentication successful for user '{Username}' ({UserId}).", user.Username, user.Id);

            return AuthenticateSuccess(user.Id, user.Username);
        }

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
            List<Claim> claims = new()
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new(ClaimTypes.Name, username),
            };

            ClaimsIdentity identity = new(claims, SchemeName);
            return new ClaimsPrincipal(identity);
        }

        private static (string username, string token)? ParseBasicCredentials(string encodedCredentials)
        {
            if (string.IsNullOrWhiteSpace(encodedCredentials))
            {
                return null;
            }

            byte[] bytes = Convert.FromBase64String(encodedCredentials);
            string decoded = StrictUtf8.GetString(bytes);
            if (decoded.Any(char.IsControl))
            {
                return null;
            }

            int idx = decoded.IndexOf(':');
            if (idx <= 0)
            {
                return null;
            }

            string username = decoded[..idx];
            string token = decoded[(idx + 1)..];
            return (username, token);
        }

        private bool TryGetBasicAuthParameter(
            string authHeader,
            out string encodedCredentials,
            out AuthenticateResult failure)
        {
            encodedCredentials = string.Empty;
            if (!AuthenticationHeaderValue.TryParse(authHeader, out AuthenticationHeaderValue? parsedHeader)
                || !parsedHeader.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogInformation("WebDAV auth: missing or non-Basic Authorization header.");
                failure = AuthenticateResult.NoResult();
                return false;
            }

            if (string.IsNullOrWhiteSpace(parsedHeader.Parameter))
            {
                Logger.LogInformation("WebDAV auth: Basic Authorization header has no credentials.");
                failure = AuthenticateResult.Fail("Invalid Authorization header.");
                return false;
            }

            encodedCredentials = parsedHeader.Parameter;
            failure = default!;
            return true;
        }

        private bool TryParseAndValidateCredentials(
            string encodedCredentials,
            out string username,
            out string token,
            out AuthenticateResult failure)
        {
            username = string.Empty;
            token = string.Empty;

            (string username, string token)? creds;
            try
            {
                creds = ParseBasicCredentials(encodedCredentials);
            }
            catch (Exception ex) when (ex is FormatException or DecoderFallbackException)
            {
                Logger.LogInformation("WebDAV auth: invalid Basic Authorization header payload.");
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
            if (!authenticationFailureLimiter.IsLimited(Request.GetTrustedClientIPAddress(), username))
            {
                return null;
            }

            Context.Items[RateLimitedContextItemKey] = true;
            Logger.LogWarning(
                "WebDAV auth: rate limited username '{Username}' from remote IP {RemoteIp}.",
                username,
                Request.GetTrustedClientIPAddress());
            return AuthenticateResult.Fail("Too many WebDAV authentication attempts.");
        }

        private void RecordAuthenticationFailure(string username)
        {
            if (authenticationFailureLimiter.RecordFailure(Request.GetTrustedClientIPAddress(), username))
            {
                Context.Items[RateLimitedContextItemKey] = true;
            }
        }

        private void ClearAuthenticationFailures(string username)
        {
            authenticationFailureLimiter.Clear(Request.GetTrustedClientIPAddress(), username);
        }

        private AuthenticateResult AuthenticateSuccess(Guid userId, string username)
        {
            ClaimsPrincipal principal = CreatePrincipal(userId, username);
            return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
        }
    }
}
