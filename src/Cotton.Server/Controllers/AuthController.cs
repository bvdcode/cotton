// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Helpers;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using EasyExtensions;
using EasyExtensions.Abstractions;
using EasyExtensions.AspNetCore.Authorization.Abstractions;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.EntityFrameworkCore.Database;
using EasyExtensions.Extensions;
using EasyExtensions.Helpers;
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
        private const int RefreshTokenLength = 64;
        private const string CookieRefreshTokenKey = "refresh_token";

        [Authorize]
        [HttpPost("/api/v1/auth/totp/confirm")]


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
            TotpSetup setup = TotpHelpers.CreateSetup("Cotton Cloud", user.Username);
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
            string accessToken = CreateAccessToken(user);
            string refreshToken = StringHelpers.CreatePseudoRandomString(RefreshTokenLength);
            RefreshToken dbToken = new()
            {
                UserId = user.Id,
                Token = refreshToken,
            };
            await _dbContext.RefreshTokens.AddAsync(dbToken);
            await _dbContext.SaveChangesAsync();
            AddRefreshTokenToCookies(refreshToken);
            return Ok(new TokenPairResponseDto()
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
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
            string newRefreshToken = StringHelpers.CreatePseudoRandomString(RefreshTokenLength);
            dbToken.RevokedAt = DateTime.UtcNow;
            RefreshToken newDbToken = new()
            {
                UserId = user.Id,
                Token = newRefreshToken,
            };
            await _dbContext.RefreshTokens.AddAsync(newDbToken);
            await _dbContext.SaveChangesAsync();
            AddRefreshTokenToCookies(newRefreshToken);
            return Ok(new TokenPairResponseDto()
            {
                AccessToken = accessToken,
                RefreshToken = newRefreshToken
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
    }
}
