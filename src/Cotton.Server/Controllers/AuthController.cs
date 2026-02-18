// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Auth;
using Cotton.Server.Handlers.Users;
using Cotton.Server.Helpers;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Server.Providers;
using Cotton.Server.Services.WebDav;
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
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

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
        INotificationsProvider _notifications) : ControllerBase
    {
        private const int WebDavTokenLength = 32;
        private const int RefreshTokenLength = 32;
        private const string CookieRefreshTokenKey = "refresh_token";

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
            await _notifications.SendWebDavTokenResetAsync(userId,
                Request.GetRemoteIPAddress(),
                Request.Headers.UserAgent);
            return Ok(token);
        }

        [Authorize]
        [HttpDelete("sessions/{sessionId}")]
        public async Task<IActionResult> RevokeSession([FromRoute] string sessionId)
        {
            var userId = User.GetUserId();
            var tokens = await _dbContext.RefreshTokens
                .Where(x => x.UserId == userId && x.SessionId == sessionId && x.RevokedAt == null)
                .ExecuteUpdateAsync(x => x.SetProperty(t => t.RevokedAt, t => DateTime.UtcNow));
            // TODO: Refuse all access tokens for this sessionId
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
            string secret = _crypto.Decrypt(user.TotpSecretEncrypted);
            bool isValid = TotpHelpers.VerifyCode(secret, request.TwoFactorCode);
            if (!isValid)
            {
                return this.ApiForbidden("Invalid two-factor authentication code");
            }
            user.IsTotpEnabled = true;
            user.TotpEnabledAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            await _notifications.SendOtpEnabledAsync(userId,
                Request.GetRemoteIPAddress(),
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
            string issuer = "Cotton Cloud";
            string account = string.IsNullOrWhiteSpace(Request.Host.Host)
                ? user.Username
                : $"{user.Username}@{Request.Host.Host}";
            TotpSetup setup = TotpHelpers.CreateSetup(issuer, account);
            user.TotpSecretEncrypted = _crypto.Encrypt(setup.SecretBase32);
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

        //[EnableRateLimiting("auth")]
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.Username == request.Username || x.Email == request.Username);
            if (user == null)
            {
                user = await TryGetNewUserAsync(request);
                if (user == null)
                {
                    return this.ApiUnauthorized("Invalid username or password");
                }
            }
            if (string.IsNullOrEmpty(user.PasswordPhc) || !_hasher.Verify(request.Password, user.PasswordPhc))
            {
                await _notifications.SendFailedLoginAttemptAsync(
                    user.Id,
                    request.Username,
                    Request.GetRemoteIPAddress(),
                    Request.Headers.UserAgent);
                return this.ApiUnauthorized("Invalid username or password");
            }
            if (user.IsTotpEnabled && string.IsNullOrWhiteSpace(request.TwoFactorCode))
            {
                return this.ApiForbidden("Two-factor authentication code is required");
            }
            if (user.IsTotpEnabled && user.TotpSecretEncrypted == null)
            {
                throw new InvalidOperationException("TOTP is enabled but secret is missing");
            }
            if (user.IsTotpEnabled && user.TotpSecretEncrypted != null)
            {
                int maxFailedAttempts = _settings.GetServerSettings().TotpMaxFailedAttempts;
                if (user.TotpFailedAttempts >= maxFailedAttempts)
                {
                    await _notifications.SendTotpLockoutAsync(user.Id,
                        maxFailedAttempts,
                        Request.GetRemoteIPAddress(),
                        Request.Headers.UserAgent);
                    return this.ApiForbidden("Maximum number of TOTP verification attempts exceeded");
                }
                string secret = _crypto.Decrypt(user.TotpSecretEncrypted);
                bool isValid = TotpHelpers.VerifyCode(secret, request.TwoFactorCode!);
                if (!isValid)
                {
                    user.TotpFailedAttempts += 1;
                    await _dbContext.SaveChangesAsync();
                    await _notifications.SendTotpFailedAttemptAsync(user.Id,
                        user.TotpFailedAttempts,
                        Request.GetRemoteIPAddress(),
                        Request.Headers.UserAgent);
                    return this.ApiForbidden("Invalid two-factor authentication code");
                }
                else
                {
                    user.TotpFailedAttempts = 0;
                }
            }
            ExtendedRefreshToken dbToken = await CreateRefreshTokenAsync(user, request.TrustDevice);
            string accessToken = CreateAccessToken(user, dbToken.SessionId!);
            await _dbContext.RefreshTokens.AddAsync(dbToken);
            await _dbContext.SaveChangesAsync();
            AddRefreshTokenToCookies(dbToken.Token, request.TrustDevice);
            await _notifications.SendSuccessfulLoginAsync(user.Id,
                Request.GetRemoteIPAddress(),
                Request.Headers.UserAgent);
            return Ok(new TokenPairResponseDto()
            {
                AccessToken = accessToken,
                RefreshToken = dbToken.Token
            });
        }

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
            var dbToken = await _dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.Token == refreshToken);
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
            ExtendedRefreshToken newDbToken = await CreateRefreshTokenAsync(
                user, dbToken.IsTrusted, dbToken.SessionId);
            await _dbContext.RefreshTokens.AddAsync(newDbToken);
            await _dbContext.SaveChangesAsync();
            AddRefreshTokenToCookies(newDbToken.Token, dbToken.IsTrusted);
            return Ok(new TokenPairResponseDto()
            {
                AccessToken = accessToken,
                RefreshToken = newDbToken.Token
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
            var dbToken = await _dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.Token == refreshToken);
            if (dbToken != null && dbToken.RevokedAt == null)
            {
                dbToken.RevokedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
            }
            Response.Cookies.Delete(CookieRefreshTokenKey);
            return Ok();
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(
            [FromBody] ForgotPasswordRequestDto request,
            CancellationToken cancellationToken)
        {
            var command = new SendPasswordResetRequest(request.UsernameOrEmail, Request);
            await _mediator.Send(command, cancellationToken);
            return Ok();
        }

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

        private async Task<User?> TryGetNewUserAsync(LoginRequest request)
        {
            bool isPublicInstance = Environment.GetEnvironmentVariable("COTTON_PUBLIC_INSTANCE") == "true";
            if (isPublicInstance)
            {
                User guest = new()
                {
                    Role = UserRole.User,
                    Username = request.Username.Trim(),
                    PasswordPhc = _hasher.Hash(request.Password),
                    WebDavTokenPhc = _hasher.Hash(request.Password),
                };
                await _dbContext.Users.AddAsync(guest);
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Created guest user {Username} on public instance", guest.Username);
                return guest;
            }

            bool hasUsers = await _dbContext.Users.AnyAsync();
            if (hasUsers)
            {
                return null;
            }

            var uptime = DateTimeOffset.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();
            if (uptime.TotalMinutes > 5)
            {
                _logger.LogWarning("Attempt to create initial admin user after uptime of {Uptime}. " +
                    "Please restart the application to enable initial admin user creation.", uptime);
                return null;
            }
            User user = new()
            {
                Role = UserRole.Admin,
                Username = request.Username.Trim(),
                PasswordPhc = _hasher.Hash(request.Password),
                WebDavTokenPhc = _hasher.Hash(request.Password),
            };
            await _dbContext.Users.AddAsync(user);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Created initial admin user: {Username}", user.Username);
            return user;
        }

        private async Task<ExtendedRefreshToken> CreateRefreshTokenAsync(
            User user,
            bool trustDevice,
            string? sessionId = null)
        {
            var lookup = await GeoIpHelpers.LookupAsync(Request.GetRemoteAddress());
            sessionId ??= StringHelpers.CreateRandomString(RefreshTokenLength);
            return new()
            {
                RevokedAt = null,
                UserId = user.Id,
                City = lookup.City,
                SessionId = sessionId,
                Region = lookup.Region,
                IsTrusted = trustDevice,
                Country = lookup.Country,
                AuthType = AuthType.Credentials,
                IpAddress = Request.GetRemoteIPAddress(),
                UserAgent = Request.Headers.UserAgent.ToString(),
                Token = StringHelpers.CreateRandomString(RefreshTokenLength),
                Device = UserAgentHelpers.GetDevice(Request.Headers.UserAgent.ToString()),
            };
        }
    }
}
