// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Abstractions;
using Cotton.Server.Auth;
using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Auth;
using Cotton.Server.Handlers.Users;
using Cotton.Server.Helpers;
using Cotton.Server.Hubs;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Server.Providers;
using Cotton.Server.Services;
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
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route(Routes.V1.Auth)]
    public class AuthController(
        IMediator _mediator,
        IStreamCipher _crypto,
        ITokenProvider _tokens,
        SettingsProvider _settings,
        CottonDbContext _dbContext,
        IPasswordHashService _hasher,
        ILogger<AuthController> _logger,
        WebDavAuthCache _webDavAuthCache,
        INotificationsProvider _notifications,
        IGeoLookupService _geoLookup,
        PasskeyService _passkeys,
        DefaultUserContentSeeder _defaultUserContentSeeder,
        ApplicationStartupClock _startupClock,
        SessionAccessTokenRevocationStore _sessionRevocations,
        IHubContext<EventHub> _eventHub) : ControllerBase
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
            var userId = User.GetUserId();
            var user = _dbContext.Users.Find(userId)
                ?? throw new EntityNotFoundException<User>();
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

        [Authorize]
        [HttpDelete("sessions/{sessionId}")]
        public async Task<IActionResult> RevokeSession(
            [FromRoute] string sessionId,
            CancellationToken cancellationToken)
        {
            var userId = User.GetUserId();
            DateTime revokedAt = DateTime.UtcNow;
            int revokedTokens = await _dbContext.RefreshTokens
                .Where(x => x.UserId == userId && x.SessionId == sessionId && x.RevokedAt == null)
                .ExecuteUpdateAsync(
                    x => x.SetProperty(t => t.RevokedAt, _ => revokedAt),
                    cancellationToken);
            if (revokedTokens > 0)
            {
                await NotifySessionRevokedAsync(userId, sessionId, cancellationToken);
            }
            return Ok();
        }

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
            return Ok(user.Adapt<UserDto>());
        }

        [Authorize]
        [HttpGet("passkeys")]
        public async Task<IActionResult> GetPasskeys(CancellationToken cancellationToken)
        {
            var userId = User.GetUserId();
            var credentials = await _passkeys.GetCredentialsAsync(userId, cancellationToken);
            return Ok(credentials);
        }

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

        [Authorize]
        [HttpDelete("passkeys/{credentialId:guid}")]
        public async Task<IActionResult> DeletePasskey(
            [FromRoute] Guid credentialId,
            CancellationToken cancellationToken)
        {
            await _passkeys.DeleteCredentialAsync(User.GetUserId(), credentialId, cancellationToken);
            return Ok();
        }

        [EnableRateLimiting(AuthRateLimitPolicies.Interactive)]
        [HttpPost("passkeys/assertion/options")]
        public async Task<IActionResult> BeginPasskeyAssertion(
            [FromBody] BeginPasskeyAssertionRequestDto request,
            CancellationToken cancellationToken)
        {
            var response = await _passkeys.BeginAssertionAsync(request.Username, cancellationToken);
            return Ok(response);
        }

        [EnableRateLimiting(AuthRateLimitPolicies.Interactive)]
        [HttpPost("passkeys/assertion/verify")]
        public async Task<IActionResult> FinishPasskeyAssertion(
            [FromBody] FinishPasskeyAssertionRequestDto request,
            CancellationToken cancellationToken)
        {
            try
            {
                User user = await _passkeys.FinishAssertionAsync(request, cancellationToken);
                return Ok(await CreateSignedInResponseAsync(user, request.TrustDevice));
            }
            catch (UnauthorizedAccessException)
            {
                return this.ApiUnauthorized("Invalid passkey");
            }
        }

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

            return Ok(await CreateSignedInResponseAsync(user, request.TrustDevice));
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

            string refreshTokenHash = HashRefreshToken(refreshToken);
            var dbToken = await _dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.Token == refreshTokenHash);
            if (dbToken == null || dbToken.RevokedAt != null)
            {
                return NotFound();
            }
            var user = await _dbContext.Users.FindAsync(dbToken.UserId);
            if (user == null)
            {
                return NotFound();
            }
            var accessToken = CreateAccessToken(user, dbToken.SessionId!);
            dbToken.RevokedAt = DateTime.UtcNow;
            var (newDbToken, newRefreshToken) = await CreateRefreshTokenAsync(
                user, dbToken.IsTrusted, dbToken.SessionId);
            await _dbContext.RefreshTokens.AddAsync(newDbToken);
            await _dbContext.SaveChangesAsync();
            AddRefreshTokenToCookies(newRefreshToken, dbToken.IsTrusted);
            AddAccessTokenToCookies(accessToken);
            return Ok(new TokenPairResponseDto()
            {
                AccessToken = accessToken,
                RefreshToken = newRefreshToken
            });
        }

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
                string refreshTokenHash = HashRefreshToken(refreshToken);
                var dbToken = await _dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.Token == refreshTokenHash);
                if (dbToken != null && dbToken.RevokedAt == null)
                {
                    dbToken.RevokedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                    await NotifySessionRevokedAsync(
                        dbToken.UserId,
                        dbToken.SessionId,
                        HttpContext.RequestAborted);
                }
            }
            Response.Cookies.Delete(CookieRefreshTokenKey);
            return Ok();
        }

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

        [Authorize]
        [HttpPost("invalidate-share-links")]
        public async Task<IActionResult> InvalidateShareLinks(CancellationToken cancellationToken)
        {
            var userId = User.GetUserId();
            await _dbContext.DownloadTokens
                .Where(dt => dt.CreatedByUserId == userId
                    && (dt.ExpiresAt == null || dt.ExpiresAt > DateTime.UtcNow))
                .ExecuteUpdateAsync(
                    s => s.SetProperty(dt => dt.ExpiresAt, DateTime.UtcNow),
                    cancellationToken);
            return Ok();
        }

        private async Task NotifySessionRevokedAsync(
            Guid userId,
            string? sessionId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            _sessionRevocations.Revoke(userId, sessionId, _tokens.TokenLifetime);
            await _eventHub.Clients
                .Group(EventHub.GetSessionGroupName(userId, sessionId))
                .SendCoreAsync(EventHub.SessionRevokedMethod, Array.Empty<object>(), cancellationToken);
        }

        private async Task<TokenPairResponseDto> CreateSignedInResponseAsync(User user, bool trustDevice)
        {
            var (dbToken, refreshToken) = await CreateRefreshTokenAsync(user, trustDevice);
            string accessToken = CreateAccessToken(user, dbToken.SessionId!);
            await _dbContext.RefreshTokens.AddAsync(dbToken);
            await _dbContext.SaveChangesAsync();
            AddRefreshTokenToCookies(refreshToken, trustDevice);
            AddAccessTokenToCookies(accessToken);
            await _notifications.SendSuccessfulLoginAsync(
                _geoLookup,
                user.Id,
                GetRequestIpAddress(),
                Request.Headers.UserAgent);

            return new TokenPairResponseDto()
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };
        }

        private string CreateAccessToken(User user, string sessionId)
        {
            return _tokens.CreateToken(x =>
            {
                return x.Add(JwtRegisteredClaimNames.Sub, user.Id.ToString())
                    .Add(JwtRegisteredClaimNames.Name, user.Username)
                    .Add(JwtRegisteredClaimNames.Sid, sessionId)
                    .Add(ClaimTypes.Name, user.Username)
                    .Add(ClaimTypes.Role, user.Role.ToString());
            });
        }

        private IPAddress GetRequestIpAddress()
        {
            return Constants.IsPublicInstance
                ? IPAddress.Loopback
                : Request.GetRemoteIPAddress();
        }

        private void AddRefreshTokenToCookies(string refreshToken, bool trustDevice)
        {
            const int yearHours = 24 * 365;
            int sessionTimeoutHours = _settings.GetServerSettings().SessionTimeoutHours;
            Response.Cookies.Append(CookieRefreshTokenKey, refreshToken, new CookieOptions
            {
                Secure = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddHours(trustDevice ? yearHours : sessionTimeoutHours)
            });
        }

        private void AddAccessTokenToCookies(string accessToken)
        {
            Response.Cookies.Append(CookieAccessTokenKey, accessToken, new CookieOptions
            {
                Secure = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.Add(_tokens.TokenLifetime)
            });
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

        private async Task<(ExtendedRefreshToken DbToken, string RefreshToken)> CreateRefreshTokenAsync(
            User user,
            bool trustDevice,
            string? sessionId = null)
        {
            IPAddress ipAddress = GetRequestIpAddress();
            var lookup = await _geoLookup.TryLookupAsync(ipAddress);
            sessionId ??= StringHelpers.CreateRandomString(RefreshTokenLength);
            string refreshToken = StringHelpers.CreateRandomString(RefreshTokenLength);
            ExtendedRefreshToken dbToken = new()
            {
                RevokedAt = null,
                UserId = user.Id,
                City = lookup?.City ?? "Unknown",
                SessionId = sessionId,
                Region = lookup?.Region ?? "Unknown",
                IsTrusted = trustDevice,
                Country = lookup?.Country ?? "Unknown",
                AuthType = AuthType.Credentials,
                IpAddress = ipAddress,
                UserAgent = Request.Headers.UserAgent.ToString(),
                Token = HashRefreshToken(refreshToken),
                Device = UserAgentHelpers.GetDevice(Request.Headers.UserAgent.ToString()),
            };
            return (dbToken, refreshToken);
        }

        private static string HashRefreshToken(string refreshToken)
        {
            return Hasher.ToHexStringHash(Hasher.HashData(Encoding.UTF8.GetBytes(refreshToken)));
        }
    }
}
