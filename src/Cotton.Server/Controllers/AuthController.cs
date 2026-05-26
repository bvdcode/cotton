// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Abstractions;
using Cotton.Server.Auth;
using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Auth;
using Cotton.Server.Handlers.Users;
using Cotton.Server.Helpers;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
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
using EasyExtensions.Extensions;
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

namespace Cotton.Server.Controllers
{
    /// <summary>
    /// Exposes HTTP endpoints for auth operations.
    /// </summary>
    [ApiController]
    [Route(Routes.V1.Auth)]
    public class AuthController(
        IMediator _mediator,
        IStreamCipher _crypto,
        SettingsProvider _settings,
        CottonDbContext _dbContext,
        IPasswordHashService _hasher,
        AuthSessionIssuer _sessionIssuer,
        ILogger<AuthController> _logger,
        WebDavAuthCache _webDavAuthCache,
        INotificationsProvider _notifications,
        IGeoLookupService _geoLookup,
        PasskeyService _passkeys,
        DefaultUserContentSeeder _defaultUserContentSeeder,
        ApplicationStartupClock _startupClock,
        RefreshTokenRevocationService _refreshTokenRevocations,
        DownloadTokenExpirationService _downloadTokenExpirations,
        IDatabaseIntegrityVerifier _integrity,
        SessionRevocationNotifier _sessionRevocationNotifier) : ControllerBase
    {
        /// <summary>
        /// Gets or sets the web dav token length.
        /// </summary>
        public const int WebDavTokenLength = 32;
        /// <summary>
        /// Gets or sets the refresh token length.
        /// </summary>
        public const int RefreshTokenLength = 32;
        /// <summary>
        /// Defines the cookie access token key.
        /// </summary>
        public const string CookieAccessTokenKey = "access_token";
        /// <summary>
        /// Defines the cookie refresh token key.
        /// </summary>
        public const string CookieRefreshTokenKey = "refresh_token";
        private static readonly EmailAddressAttribute EmailValidator = new();

        /// <summary>
        /// Gets web dav token.
        /// </summary>
        [Authorize]
        [HttpGet("webdav/token")]
        public async Task<IActionResult> GetWebDavToken()
        {
            var userId = User.GetUserId();
            var user = _dbContext.Users.Find(userId)
                ?? throw new EntityNotFoundException<User>();
            _integrity.RequireValid(_dbContext, user, "auth.webdav-token");
            string token = StringHelpers.CreateRandomString(WebDavTokenLength);
            user.WebDavTokenPhc = _hasher.Hash(token);
            await _dbContext.SaveChangesAsync();
            _webDavAuthCache.BumpUsernameCacheVersion(user.Username);
            await _notifications.SendWebDavTokenResetAsync(
                _geoLookup,
                userId,
                GetRequestIpAddress(),
                Request.Headers.UserAgent);
            return Ok(token);
        }

        /// <summary>
        /// Revokes session.
        /// </summary>
        [Authorize]
        [HttpDelete("sessions/{sessionId}")]
        public async Task<IActionResult> RevokeSession(
            [FromRoute] string sessionId,
            CancellationToken cancellationToken)
        {
            var userId = User.GetUserId();
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

        /// <summary>
        /// Gets sessions.
        /// </summary>
        [Authorize]
        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions()
        {
            var userId = User.GetUserId();
            string currentSessionId = User.Claims.FirstOrDefault(x =>
                x.Type == JwtRegisteredClaimNames.Sid)?.Value ?? string.Empty;
            GetSessionsQuery query = new(userId, currentSessionId);
            var sessions = await _mediator.Send(query);
            return Ok(sessions);
        }

        /// <summary>
        /// Disables totp.
        /// </summary>
        [Authorize]
        [HttpDelete("totp/disable")]
        public async Task<IActionResult> DisableTotp([FromBody] DisableTotpRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return this.ApiBadRequest("Password is required");
            }
            var userId = User.GetUserId();
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
            {
                return this.ApiUnauthorized("User not found");
            }
            _integrity.RequireValid(_dbContext, user, "auth.disable-totp");
            if (string.IsNullOrEmpty(user.PasswordPhc) || !_hasher.Verify(request.Password, user.PasswordPhc))
            {
                return this.ApiForbidden("Invalid password");
            }
            if (!user.IsTotpEnabled)
            {
                return this.ApiConflict("TOTP is not enabled for this user");
            }
            user.IsTotpEnabled = false;
            user.TotpSecretEncrypted = null;
            user.TotpEnabledAt = null;
            await _dbContext.SaveChangesAsync();
            await _notifications.SendOtpDisabledAsync(
                _geoLookup,
                userId,
                GetRequestIpAddress(),
                Request.Headers.UserAgent);
            return Ok();
        }

        /// <summary>
        /// Confirms totp.
        /// </summary>
        [Authorize]
        [HttpPost("totp/confirm")]
        public async Task<IActionResult> ConfirmTotp([FromBody] ConfirmTotpRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.TwoFactorCode))
            {
                return this.ApiBadRequest("Two-factor authentication code is required");
            }
            var userId = User.GetUserId();
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
            {
                return this.ApiUnauthorized("User not found");
            }
            _integrity.RequireValid(_dbContext, user, "auth.confirm-totp");
            if (user.IsTotpEnabled)
            {
                return this.ApiConflict("TOTP is already enabled for this user");
            }
            if (user.TotpSecretEncrypted == null)
            {
                return this.ApiBadRequest("TOTP setup has not been initiated for this user");
            }
            string secret = _crypto.DecryptString(user.TotpSecretEncrypted);
            bool isValid = TotpHelpers.VerifyCode(secret, request.TwoFactorCode);
            if (!isValid)
            {
                return this.ApiForbidden("Invalid two-factor authentication code");
            }
            user.IsTotpEnabled = true;
            user.TotpEnabledAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            await _notifications.SendOtpEnabledAsync(
                _geoLookup,
                userId,
                GetRequestIpAddress(),
                Request.Headers.UserAgent);
            return Ok();
        }

        /// <summary>
        /// Sets up totp.
        /// </summary>
        [Authorize]
        [HttpPost("totp/setup")]
        public async Task<IActionResult> SetupTotp()
        {
            var userId = User.GetUserId();
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
            {
                return this.ApiUnauthorized("User not found");
            }
            _integrity.RequireValid(_dbContext, user, "auth.setup-totp");
            if (user.IsTotpEnabled)
            {
                return this.ApiConflict("TOTP is already enabled for this user");
            }
            string issuer = Constants.ShortProductName;
            string account = string.IsNullOrWhiteSpace(Request.Host.Host)
                ? user.Username
                : $"{user.Username}@{Request.Host.Host}";
            TotpSetup setup = TotpHelpers.CreateSetup(issuer, account);
            user.TotpSecretEncrypted = _crypto.EncryptString(setup.SecretBase32);
            await _dbContext.SaveChangesAsync();
            return Ok(setup);
        }

        /// <summary>
        /// Returns the current authenticated user.
        /// </summary>
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var userId = User.GetUserId();
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
            {
                return this.ApiUnauthorized("User not found");
            }
            _integrity.RequireValid(_dbContext, user, "auth.me");
            return Ok(user.Adapt<UserDto>());
        }

        /// <summary>
        /// Gets passkeys.
        /// </summary>
        [Authorize]
        [HttpGet("passkeys")]
        public async Task<IActionResult> GetPasskeys(CancellationToken cancellationToken)
        {
            var userId = User.GetUserId();
            var credentials = await _passkeys.GetCredentialsAsync(userId, cancellationToken);
            return Ok(credentials);
        }

        /// <summary>
        /// Begins passkey registration.
        /// </summary>
        [Authorize]
        [HttpPost("passkeys/registration/options")]
        public async Task<IActionResult> BeginPasskeyRegistration(
            [FromBody] BeginPasskeyRegistrationRequestDto request,
            CancellationToken cancellationToken)
        {
            var response = await _passkeys.BeginRegistrationAsync(
                User.GetUserId(),
                request.Name,
                cancellationToken);
            return Ok(response);
        }

        /// <summary>
        /// Finishes passkey registration.
        /// </summary>
        [Authorize]
        [HttpPost("passkeys/registration/verify")]
        public async Task<IActionResult> FinishPasskeyRegistration(
            [FromBody] FinishPasskeyRegistrationRequestDto request,
            CancellationToken cancellationToken)
        {
            var response = await _passkeys.FinishRegistrationAsync(
                User.GetUserId(),
                request,
                cancellationToken);
            return Ok(response);
        }

        /// <summary>
        /// Renames passkey.
        /// </summary>
        [Authorize]
        [HttpPut("passkeys/{credentialId:guid}")]
        public async Task<IActionResult> RenamePasskey(
            [FromRoute] Guid credentialId,
            [FromBody] RenamePasskeyRequestDto request,
            CancellationToken cancellationToken)
        {
            var response = await _passkeys.RenameCredentialAsync(
                User.GetUserId(),
                credentialId,
                request.Name,
                cancellationToken);
            return Ok(response);
        }

        /// <summary>
        /// Deletes passkey.
        /// </summary>
        [Authorize]
        [HttpDelete("passkeys/{credentialId:guid}")]
        public async Task<IActionResult> DeletePasskey(
            [FromRoute] Guid credentialId,
            CancellationToken cancellationToken)
        {
            await _passkeys.DeleteCredentialAsync(User.GetUserId(), credentialId, cancellationToken);
            return Ok();
        }

        /// <summary>
        /// Begins passkey assertion.
        /// </summary>
        [EnableRateLimiting(AuthRateLimitPolicies.Interactive)]
        [HttpPost("passkeys/assertion/options")]
        public async Task<IActionResult> BeginPasskeyAssertion(
            [FromBody] BeginPasskeyAssertionRequestDto request,
            CancellationToken cancellationToken)
        {
            var response = await _passkeys.BeginAssertionAsync(request.Username, cancellationToken);
            return Ok(response);
        }

        /// <summary>
        /// Finishes passkey assertion.
        /// </summary>
        [EnableRateLimiting(AuthRateLimitPolicies.Interactive)]
        [HttpPost("passkeys/assertion/verify")]
        public async Task<IActionResult> FinishPasskeyAssertion(
            [FromBody] FinishPasskeyAssertionRequestDto request,
            CancellationToken cancellationToken)
        {
            try
            {
                User user = await _passkeys.FinishAssertionAsync(request, cancellationToken);
                return Ok(await CreateSignedInResponseAsync(user, request.TrustDevice, AuthType.Passkey));
            }
            catch (UnauthorizedAccessException)
            {
                return this.ApiUnauthorized("Invalid passkey");
            }
        }

        /// <summary>
        /// Authenticates a user and issues access and refresh tokens.
        /// </summary>
        [EnableRateLimiting(AuthRateLimitPolicies.Interactive)]
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var user = await GetUserOrTryGetNewAsync(request);
            if (user == null)
            {
                return this.ApiUnauthorized("Invalid username or password");
            }

            bool passwordOk = await VerifyPasswordOrNotifyAsync(user, request);
            if (!passwordOk)
            {
                return this.ApiUnauthorized("Invalid username or password");
            }

            var totpFailure = await ValidateTotpOrGetFailureAsync(user, request);
            if (totpFailure != null)
            {
                return totpFailure;
            }

            return Ok(await CreateSignedInResponseAsync(user, request.TrustDevice, AuthType.Credentials));
        }

        private async Task<User?> GetUserOrTryGetNewAsync(LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username))
            {
                return null;
            }

            request.Username = request.Username.Trim();
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.Username == request.Username || x.Email == request.Username);
            if (user != null)
            {
                _integrity.RequireValid(_dbContext, user, "auth.login");
                return user;
            }

            return await TryGetNewUserAsync(request);
        }

        private async Task<bool> VerifyPasswordOrNotifyAsync(User user, LoginRequest request)
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

        private async Task<IActionResult?> ValidateTotpOrGetFailureAsync(User user, LoginRequest request)
        {
            if (!user.IsTotpEnabled)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(request.TwoFactorCode))
            {
                return this.ApiForbidden("Two-factor authentication code is required");
            }

            if (user.TotpSecretEncrypted == null)
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

            string secret = _crypto.DecryptString(user.TotpSecretEncrypted);
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

        /// <summary>
        /// Gets refresh token.
        /// </summary>
        [EnableRateLimiting(AuthRateLimitPolicies.Refresh)]
        [HttpPost("refresh")]
        public async Task<IActionResult> GetRefreshToken([FromQuery] string? refreshToken = null)
        {
            if (string.IsNullOrEmpty(refreshToken))
            {
                if (Request.Cookies.TryGetValue(CookieRefreshTokenKey, out var cookieToken))
                {
                    refreshToken = cookieToken;
                }
            }
            if (string.IsNullOrEmpty(refreshToken))
            {
                return NotFound();
            }

            string refreshTokenHash = AuthSessionIssuer.HashRefreshToken(refreshToken);
            var dbToken = await _dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.Token == refreshTokenHash);
            if (dbToken == null || dbToken.RevokedAt != null)
            {
                return NotFound();
            }
            _integrity.RequireValid(_dbContext, dbToken, "auth.refresh-token");
            var user = await _dbContext.Users.FindAsync(dbToken.UserId);
            if (user == null)
            {
                return NotFound();
            }
            _integrity.RequireValid(_dbContext, user, "auth.refresh-user");
            var accessToken = _sessionIssuer.CreateAccessToken(user, dbToken.SessionId!);
            dbToken.RevokedAt = DateTime.UtcNow;
            var (newDbToken, newRefreshToken) = await _sessionIssuer.CreateRefreshTokenAsync(
                user,
                dbToken.IsTrusted,
                dbToken.AuthType,
                dbToken.SessionId);
            await _dbContext.RefreshTokens.AddAsync(newDbToken);
            await _dbContext.SaveChangesAsync();
            _sessionIssuer.AddRefreshTokenToCookies(newRefreshToken, dbToken.IsTrusted);
            _sessionIssuer.AddAccessTokenToCookies(accessToken);
            return Ok(new TokenPairResponseDto()
            {
                AccessToken = accessToken,
                RefreshToken = newRefreshToken
            });
        }

        /// <summary>
        /// Revokes the current refresh token and clears auth cookies.
        /// </summary>
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromQuery] string? refreshToken = null)
        {
            if (string.IsNullOrEmpty(refreshToken))
            {
                if (Request.Cookies.TryGetValue(CookieRefreshTokenKey, out var cookieToken))
                {
                    refreshToken = cookieToken;
                }
            }
            if (!string.IsNullOrEmpty(refreshToken))
            {
                string refreshTokenHash = AuthSessionIssuer.HashRefreshToken(refreshToken);
                var dbToken = await _dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.Token == refreshTokenHash);
                if (dbToken != null && dbToken.RevokedAt == null)
                {
                    _integrity.RequireValid(_dbContext, dbToken, "auth.logout");
                    dbToken.RevokedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                    await _sessionRevocationNotifier.NotifyRevokedAsync(
                        dbToken.UserId,
                        dbToken.SessionId,
                        HttpContext.RequestAborted);
                }
            }
            Response.Cookies.Delete(CookieRefreshTokenKey);
            return Ok();
        }

        /// <summary>
        /// Starts the password reset flow without revealing whether the account exists.
        /// </summary>
        [EnableRateLimiting(AuthRateLimitPolicies.Interactive)]
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(
            [FromBody] ForgotPasswordRequestDto request,
            CancellationToken cancellationToken)
        {
            var command = new SendPasswordResetRequest(request.UsernameOrEmail, Request);
            await _mediator.Send(command, cancellationToken);
            return Ok();
        }

        /// <summary>
        /// Clears the cached value so it will be resolved again.
        /// </summary>
        [EnableRateLimiting(AuthRateLimitPolicies.Interactive)]
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(
            [FromBody] ResetPasswordRequestDto request,
            CancellationToken cancellationToken)
        {
            var command = new ConfirmPasswordResetRequest(request.Token, request.NewPassword);
            await _mediator.Send(command, cancellationToken);
            return Ok();
        }

        /// <summary>
        /// Invalidates every share link owned by the current user.
        /// </summary>
        [Authorize]
        [HttpPost("invalidate-share-links")]
        public async Task<IActionResult> InvalidateShareLinks(CancellationToken cancellationToken)
        {
            var userId = User.GetUserId();
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
                : Request.GetRemoteIPAddress();
        }

        private async Task<User?> TryGetNewUserAsync(LoginRequest request)
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
                    WebDavTokenPhc = _hasher.Hash(request.Password),
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
                WebDavTokenPhc = _hasher.Hash(request.Password),
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
