// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Helpers;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Server.Providers;
using EasyExtensions;
using EasyExtensions.Abstractions;
using EasyExtensions.AspNetCore.Authorization.Abstractions;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.EntityFrameworkCore.Database;
using EasyExtensions.Extensions;
using EasyExtensions.Helpers;
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
    public class AuthController(
        IStreamCipher _crypto,
        ITokenProvider _tokens,
        SettingsProvider _settings,
        CottonDbContext _dbContext,
        ILogger<AuthController> _logger,
        IPasswordHashService _hasher) : ControllerBase
    {
        private const int RefreshTokenLength = 32;
        private const string CookieRefreshTokenKey = "refresh_token";

        [Authorize]
        [HttpDelete("/api/v1/auth/sessions/{sessionId}")]
        public async Task<IActionResult> RevokeSession([FromRoute] string sessionId)
        {
            var userId = User.GetUserId();
            var tokens = await _dbContext.RefreshTokens
                .Where(x => x.UserId == userId && x.SessionId == sessionId && x.RevokedAt == null)
                .ExecuteUpdateAsync(x => x.SetProperty(t => t.RevokedAt, t => DateTime.UtcNow));
            return Ok();
        }

        [Authorize]
        [HttpGet("/api/v1/auth/sessions")]
        public async Task<IActionResult> GetSessions()
        {
            var userId = User.GetUserId();
            var tokens = await _dbContext.RefreshTokens
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.SessionId != null)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            List<SessionDto> response = [.. tokens
                .GroupBy(x => x.SessionId!)
                .Select(g =>
                {
                    var latestActive = g
                        .Where(x => x.RevokedAt == null)
                        .OrderByDescending(x => x.CreatedAt)
                        .FirstOrDefault();

                    var latestAny = g
                        .OrderByDescending(x => x.CreatedAt)
                        .First();

                    var source = latestActive ?? latestAny;
                    var earliestCreatedAt = g.Min(x => x.CreatedAt);
                    var latestCreatedAt = g.Max(x => x.CreatedAt);

                    return new SessionDto
                    {
                        SessionId = g.Key,
                        IpAddress = source.IpAddress.ToString(),
                        UserAgent = source.UserAgent,
                        AuthType = source.AuthType,
                        Country = source.Country ?? "Unknown",
                        Region = source.Region ?? "Unknown",
                        City = source.City ?? "Unknown",
                        Device = source.Device ?? "Unknown",
                        RefreshTokenCount = g.Count(),
                        TotalSessionDuration = latestCreatedAt - earliestCreatedAt
                    };
                })
                .OrderByDescending(x => x.TotalSessionDuration)];

            return Ok(response);
        }

        [Authorize]
        [HttpPost("/api/v1/auth/totp/confirm")]
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
            return Ok();
        }


        [Authorize]
        [HttpPost("/api/v1/auth/totp/setup")]
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
        [HttpGet("/api/v1/auth/me")]
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
        [HttpPost("/api/v1/auth/login")]
        public async Task<IActionResult> Login(LoginRequestDto request)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Username == request.Username);
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
                    return this.ApiForbidden("Maximum number of TOTP verification attempts exceeded");
                }
                string secret = _crypto.Decrypt(user.TotpSecretEncrypted);
                bool isValid = TotpHelpers.VerifyCode(secret, request.TwoFactorCode!);
                if (!isValid)
                {
                    user.TotpFailedAttempts += 1;
                    await _dbContext.SaveChangesAsync();
                    return this.ApiForbidden("Invalid two-factor authentication code");
                }
                else
                {
                    user.TotpFailedAttempts = 0;
                }
            }
            string accessToken = CreateAccessToken(user);
            ExtendedRefreshToken dbToken = await CreateRefreshTokenAsync(user);
            await _dbContext.RefreshTokens.AddAsync(dbToken);
            await _dbContext.SaveChangesAsync();
            AddRefreshTokenToCookies(dbToken.Token);
            return Ok(new TokenPairResponseDto()
            {
                AccessToken = accessToken,
                RefreshToken = dbToken.Token
            });
        }

        [HttpPost("/api/v1/auth/refresh")]
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
            var accessToken = CreateAccessToken(user);
            dbToken.RevokedAt = DateTime.UtcNow;
            ExtendedRefreshToken newDbToken = await CreateRefreshTokenAsync(user, dbToken.SessionId);
            await _dbContext.RefreshTokens.AddAsync(newDbToken);
            await _dbContext.SaveChangesAsync();
            AddRefreshTokenToCookies(newDbToken.Token);
            return Ok(new TokenPairResponseDto()
            {
                AccessToken = accessToken,
                RefreshToken = newDbToken.Token
            });
        }

        [HttpPost("/api/v1/auth/logout")]
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

        private string CreateAccessToken(User user)
        {
            return _tokens.CreateToken(x =>
            {
                return x.Add(JwtRegisteredClaimNames.Sub, user.Id.ToString())
                    .Add(JwtRegisteredClaimNames.Name, user.Username)
                    .Add(ClaimTypes.Name, user.Username)
                    .Add(ClaimTypes.Role, user.Role.ToString());
            });
        }

        private void AddRefreshTokenToCookies(string refreshToken)
        {
            int sessionTimeoutHours = _settings.GetServerSettings().SessionTimeoutHours;
            Response.Cookies.Append(CookieRefreshTokenKey, refreshToken, new CookieOptions
            {
                Secure = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddHours(sessionTimeoutHours)
            });
        }

        private async Task<User?> TryGetNewUserAsync(LoginRequestDto request)
        {
            bool isPublicInstance = Environment.GetEnvironmentVariable("COTTON_PUBLIC_INSTANCE") == "true";
            if (isPublicInstance)
            {
                User guest = new()
                {
                    Role = UserRole.User,
                    Username = request.Username.Trim(),
                    PasswordPhc = _hasher.Hash(request.Password)
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
                _logger.LogWarning("Attempt to create initial admin user after uptime of {Uptime}. Please restart the application to enable initial admin user creation.", uptime);
                return null;
            }
            User user = new()
            {
                Role = UserRole.Admin,
                Username = request.Username.Trim(),
                PasswordPhc = _hasher.Hash(request.Password)
            };
            await _dbContext.Users.AddAsync(user);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Created initial admin user: {Username}", user.Username);
            return user;
        }

        private async Task<ExtendedRefreshToken> CreateRefreshTokenAsync(User user, string? sessionId = null)
        {
            var lookup = await GeoIpHelpers.LookupAsync(Request.GetRemoteAddress());
            sessionId ??= StringHelpers.CreateRandomString(32);
            return new()
            {
                RevokedAt = null,
                UserId = user.Id,
                City = lookup.City,
                SessionId = sessionId,
                Region = lookup.Region,
                Country = lookup.Country,
                AuthType = AuthType.Credentials,
                IpAddress = Request.GetRemoteIPAddress(),
                UserAgent = Request.Headers.UserAgent.ToString(),
                Token = StringHelpers.CreatePseudoRandomString(RefreshTokenLength),
                Device = UserAgentHelpers.GetDevice(Request.Headers.UserAgent.ToString()),
            };
        }
    }
}
