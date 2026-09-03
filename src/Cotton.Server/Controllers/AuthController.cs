// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Crypto;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Abstractions;
using Cotton.Server.Auth;
using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Auth;
using Cotton.Server.Helpers;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Services.WebDav;
using Cotton.Validators;
using EasyExtensions;
using EasyExtensions.Abstractions;
using EasyExtensions.AspNetCore.Authorization.Abstractions;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.EntityFrameworkCore.Database;
using EasyExtensions.Helpers;
using EasyExtensions.Mediator;
using EasyExtensions.Models.Enums;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using CottonLoginRequestDto = Cotton.Auth.LoginRequestDto;
using CottonStreamCipher = Cotton.Crypto.IStreamCipher;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route(Routes.V1.Auth)]
    public class AuthController(
        IMediator _mediator,
        CottonStreamCipher _crypto,
        SettingsProvider _settings,
        CottonDbContext _dbContext,
        IPasswordHashService _hasher,
        AuthSessionIssuer _sessionIssuer,
        ILogger<AuthController> _logger,
        WebDavAuthCache _webDavAuthCache,
        INotificationsProvider _notifications,
        IGeoLookupService _geoLookup,
        DefaultUserContentSeeder _defaultUserContentSeeder,
        ApplicationStartupClock _startupClock,
        RefreshTokenRevocationService _refreshTokenRevocations,
        DownloadTokenExpirationService _downloadTokenExpirations,
        IDatabaseIntegrityVerifier _integrity,
        SessionRevocationNotifier _sessionRevocationNotifier) : ControllerBase
    {
        public const int WebDavTokenLength = 32;

        public const int RefreshTokenLength = 32;

        public const string CookieAccessTokenKey = "access_token";

        public const string CookieRefreshTokenKey = "refresh_token";
        private static readonly EmailAddressAttribute EmailValidator = new();

        [Authorize]
        [HttpGet("webdav/token")]
        public async Task<IActionResult> GetWebDavToken()
        {
            Guid userId = User.GetUserId();
            User user = await _dbContext.Users.FindAsync(userId)
                ?? throw new EntityNotFoundException<User>();
            _integrity.RequireValid(_dbContext, user, "auth.webdav-token");
            string token = StringHelpers.CreateRandomString(WebDavTokenLength);
            user.WebDavTokenPhc = _hasher.Hash(token);
            await _dbContext.SaveChangesAsync();
            _webDavAuthCache.BumpUsernameCacheVersion(user.Username);
            await _notifications.SendWebDavTokenResetAsync(
                _geoLookup,
                _settings,
                _logger,
                userId,
                GetRequestIpAddress(),
                Request.Headers.UserAgent);
            return Ok(token);
        }

        [Authorize]
        [HttpDelete("sessions/{sessionId}")]
        public async Task<IActionResult> RevokeSession(
            [FromRoute] string sessionId,
            CancellationToken cancellationToken)
        {
            Guid userId = User.GetUserId();
            DateTime revokedAt = DateTime.UtcNow;
            RefreshTokenRevocationResult revocation = await _refreshTokenRevocations.RevokeSessionAsync(
                userId,
                sessionId,
                revokedAt,
                cancellationToken);
            if (revocation.RevokedTokens > 0)
            {
                await _sessionRevocationNotifier.NotifyRevokedAsync(
                    userId,
                    revocation.SessionIds,
                    cancellationToken);
            }
            return Ok();
        }

        [Authorize]
        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions()
        {
            Guid userId = User.GetUserId();
            string currentSessionId = User.Claims.FirstOrDefault(x =>
                x.Type == JwtRegisteredClaimNames.Sid)?.Value ?? string.Empty;
            GetSessionsQuery query = new(userId, currentSessionId);
            IEnumerable<SessionDto> sessions = await _mediator.Send(query);
            return Ok(sessions);
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            Guid userId = User.GetUserId();
            User? user = await _dbContext.Users.FindAsync(userId);
            if (user is null)
            {
                return this.ApiUnauthorized("User not found");
            }
            _integrity.RequireValid(_dbContext, user, "auth.me");
            return Ok(user.Adapt<UserDto>());
        }

        [EnableRateLimiting(AuthRateLimitPolicies.Interactive)]
        [HttpPost("login")]
        public async Task<IActionResult> Login(CottonLoginRequestDto request)
        {
            User? user = await GetUserOrTryGetNewAsync(request);
            if (user is null)
            {
                return this.ApiUnauthorized("Invalid username or password");
            }

            bool passwordOk = await VerifyPasswordOrNotifyAsync(user, request);
            if (!passwordOk)
            {
                return this.ApiUnauthorized("Invalid username or password");
            }

            IActionResult? totpFailure = await ValidateTotpOrGetFailureAsync(user, request);
            if (totpFailure is not null)
            {
                return totpFailure;
            }

            return Ok(await CreateSignedInResponseAsync(user, request.TrustDevice, AuthType.Credentials));
        }

        private async Task<User?> GetUserOrTryGetNewAsync(CottonLoginRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Username))
            {
                return null;
            }

            request.Username = request.Username.Trim();
            User? user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.Username == request.Username || x.Email == request.Username);
            if (user is not null)
            {
                _integrity.RequireValid(_dbContext, user, "auth.login");
                return user;
            }

            return await TryGetNewUserAsync(request);
        }

        private async Task<bool> VerifyPasswordOrNotifyAsync(User user, CottonLoginRequestDto request)
        {
            if (string.IsNullOrEmpty(user.PasswordPhc) || !_hasher.Verify(request.Password, user.PasswordPhc))
            {
                await _notifications.SendFailedLoginAttemptAsync(
                    _geoLookup,
                    user.Id,
                    request.Username,
                    GetRequestIpAddress(),
                    Request.Headers.UserAgent);
                return false;
            }

            return true;
        }

        private async Task<IActionResult?> ValidateTotpOrGetFailureAsync(User user, CottonLoginRequestDto request)
        {
            if (!user.IsTotpEnabled)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(request.TwoFactorCode))
            {
                return this.ApiForbidden("Two-factor authentication code is required");
            }

            if (user.TotpSecretEncrypted is null)
            {
                throw new InvalidOperationException("TOTP is enabled but secret is missing");
            }

            int maxFailedAttempts = _settings.GetServerSettings().TotpMaxFailedAttempts;
            if (user.TotpFailedAttempts >= maxFailedAttempts)
            {
                await _notifications.SendTotpLockoutAsync(
                    _geoLookup,
                    user.Id,
                    maxFailedAttempts,
                    GetRequestIpAddress(),
                    Request.Headers.UserAgent);
                return this.ApiForbidden("Maximum number of TOTP verification attempts exceeded");
            }

            string secret = await _crypto.DecryptStringAsync(
                user.TotpSecretEncrypted,
                HttpContext.RequestAborted);
            bool isValid = TotpHelpers.VerifyCode(secret, request.TwoFactorCode);
            if (!isValid)
            {
                user.TotpFailedAttempts += 1;
                await _dbContext.SaveChangesAsync();
                await _notifications.SendTotpFailedAttemptAsync(
                    _geoLookup,
                    user.Id,
                    user.TotpFailedAttempts,
                    GetRequestIpAddress(),
                    Request.Headers.UserAgent);
                return this.ApiForbidden("Invalid two-factor authentication code");
            }

            user.TotpFailedAttempts = 0;
            return null;
        }

        [EnableRateLimiting(AuthRateLimitPolicies.Refresh)]
        [HttpPost("refresh")]
        public async Task<IActionResult> GetRefreshToken([FromQuery] string? refreshToken = null)
        {
            if (string.IsNullOrEmpty(refreshToken))
            {
                if (Request.Cookies.TryGetValue(CookieRefreshTokenKey, out string? cookieToken))
                {
                    refreshToken = cookieToken;
                }
            }
            if (string.IsNullOrEmpty(refreshToken))
            {
                return NotFound();
            }

            string refreshTokenHash = AuthSessionIssuer.HashRefreshToken(refreshToken);
            ExtendedRefreshToken? dbToken = await _dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.Token == refreshTokenHash);
            if (dbToken is null || dbToken.RevokedAt is not null)
            {
                return NotFound();
            }
            _integrity.RequireValid(_dbContext, dbToken, "auth.refresh-token");
            User? user = await _dbContext.Users.FindAsync(dbToken.UserId);
            if (user is null)
            {
                return NotFound();
            }
            _integrity.RequireValid(_dbContext, user, "auth.refresh-user");
            string accessToken = _sessionIssuer.CreateAccessToken(user, dbToken.SessionId!);
            dbToken.RevokedAt = DateTime.UtcNow;
            var (newDbToken, newRefreshToken) = await _sessionIssuer.CreateRefreshTokenAsync(
                user,
                dbToken.IsTrusted,
                dbToken.AuthType,
                dbToken.SessionId);
            await _dbContext.RefreshTokens.AddAsync(newDbToken);
            await _dbContext.SaveChangesAsync();
            _sessionIssuer.AddRefreshTokenToCookies(newRefreshToken, dbToken.IsTrusted);
            return Ok(new SessionRestoreResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = newRefreshToken,
                User = user.Adapt<UserDto>()
            });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromQuery] string? refreshToken = null)
        {
            if (string.IsNullOrEmpty(refreshToken))
            {
                if (Request.Cookies.TryGetValue(CookieRefreshTokenKey, out string? cookieToken))
                {
                    refreshToken = cookieToken;
                }
            }
            if (!string.IsNullOrEmpty(refreshToken))
            {
                string refreshTokenHash = AuthSessionIssuer.HashRefreshToken(refreshToken);
                ExtendedRefreshToken? dbToken = await _dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.Token == refreshTokenHash);
                if (dbToken is not null && dbToken.RevokedAt is null)
                {
                    _integrity.RequireValid(_dbContext, dbToken, "auth.logout");
                    RefreshTokenRevocationResult revocation = await _refreshTokenRevocations.RevokeSessionAsync(
                        dbToken.UserId,
                        dbToken.SessionId!,
                        DateTime.UtcNow,
                        HttpContext.RequestAborted);
                    await _sessionRevocationNotifier.NotifyRevokedAsync(
                        dbToken.UserId,
                        revocation.SessionIds,
                        HttpContext.RequestAborted);
                }
            }
            Response.Cookies.Delete(CookieRefreshTokenKey);
            Response.Cookies.Delete(CookieAccessTokenKey);
            return Ok();
        }

        [Authorize]
        [HttpPost("invalidate-share-links")]
        public async Task<IActionResult> InvalidateShareLinks(CancellationToken cancellationToken)
        {
            Guid userId = User.GetUserId();
            await _downloadTokenExpirations.ExpireActiveTokensCreatedByUserAsync(
                userId,
                DateTime.UtcNow,
                cancellationToken);
            return Ok();
        }

        private async Task<TokenPairResponseDto> CreateSignedInResponseAsync(User user, bool trustDevice, AuthType authType)
        {
            return await _sessionIssuer.SignInAsync(user, trustDevice, authType, HttpContext.RequestAborted);
        }

        private IPAddress GetRequestIpAddress()
        {
            return Constants.IsPublicInstance
                ? IPAddress.Loopback
                : Request.GetTrustedClientIPAddress();
        }

        private async Task<User?> TryGetNewUserAsync(CottonLoginRequestDto request)
        {
            string login = request.Username.Trim();
            string? email = null;
            string username;

            if (EmailValidator.IsValid(login))
            {
                email = login;
                username = await UsernameHelpers.BuildAvailableUsernameFromEmailAsync(_dbContext, login);
            }
            else if (!UsernameValidator.TryNormalizeAndValidate(login, out username, out _))
            {
                return null;
            }

            bool isPublicInstance = Constants.IsPublicInstance;
            if (isPublicInstance)
            {
                User guest = new()
                {
                    Email = email,
                    Username = username,
                    Role = UserRole.User,
                    FirstName = NormalizeOptionalName(request.FirstName),
                    LastName = NormalizeOptionalName(request.LastName),
                    PasswordPhc = _hasher.Hash(request.Password),
                    WebDavTokenPhc = _hasher.Hash(StringHelpers.CreateRandomString(WebDavTokenLength)),
                };
                await _dbContext.Users.AddAsync(guest);
                await _dbContext.SaveChangesAsync();
                await _defaultUserContentSeeder.SeedAsync(guest.Id);
                _logger.LogInformation("Created guest user {Username} on public instance", guest.Username);
                return guest;
            }

            bool hasUsers = await _dbContext.Users.AnyAsync();
            if (hasUsers)
            {
                return null;
            }

            if (_startupClock.Uptime.TotalMinutes > Constants.AdminAutocreateMinutesDelay)
            {
                string errorMessage = $"Initial admin user creation is disabled after " +
                    Constants.AdminAutocreateMinutesDelay + " minutes of uptime. " +
                    "Please restart the application/container to enable it.";
                _logger.LogWarning("{msg}", errorMessage);
                throw new BadRequestException<User>(errorMessage);
            }
            User user = new()
            {
                Email = email,
                Username = username,
                Role = UserRole.Admin,
                PasswordPhc = _hasher.Hash(request.Password),
                WebDavTokenPhc = _hasher.Hash(StringHelpers.CreateRandomString(WebDavTokenLength)),
            };
            await _dbContext.Users.AddAsync(user);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Created initial admin user: {Username}", user.Username);
            return user;
        }

        private static string? NormalizeOptionalName(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
