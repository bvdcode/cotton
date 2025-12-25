// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models.Enums;
using Cotton.Server.Services;
using EasyExtensions;
using EasyExtensions.Abstractions;
using EasyExtensions.AspNetCore.Authorization.Abstractions;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.EntityFrameworkCore.Database;
using EasyExtensions.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class AuthController(
        ITokenProvider _tokens,
        SettingsProvider _settings,
        CottonDbContext _dbContext,
        ILogger<AuthController> _logger,
        IPasswordHashService _hasher) : ControllerBase
    {
        private const int RefreshTokenLength = 64;
        private const string CookieRefreshTokenKey = "refresh_token";

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
            return Ok(new
            {
                user.Id,
                user.Username,
                DisplayName = user.Username,
            });
        }

        [HttpPost("/api/v1/auth/login")]
        public async Task<IActionResult> Login(LoginRequestDto request)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Username == request.Username);
            if (user == null)
            {
                bool hasUsers = await _dbContext.Users.AnyAsync();
                if (hasUsers)
                {
                    return this.ApiUnauthorized("Invalid username or password");
                }
                user = new()
                {
                    Role = UserRole.Admin,
                    Username = request.Username.Trim(),
                    PasswordPhc = _hasher.Hash(request.Password)
                };
                await _dbContext.Users.AddAsync(user);
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Created initial admin user: {Username}", user.Username);
            }
            if (string.IsNullOrEmpty(user.PasswordPhc) || !_hasher.Verify(request.Password, user.PasswordPhc))
            {
                return this.ApiUnauthorized("Invalid username or password");
            }
            var accessToken = _tokens.CreateToken(x => x.Add("sub", user.Id.ToString()));
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
            var accessToken = _tokens.CreateToken(x => x.Add("sub", user.Id.ToString()));
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

        private void AddRefreshTokenToCookies(string refreshToken)
        {
            int sessionTimeoutHours = _settings.GetServerSettings().SessionTimeoutHours;
            Response.Cookies.Append(CookieRefreshTokenKey, refreshToken, new CookieOptions
            {
                Secure = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(sessionTimeoutHours)
            });
        }
    }
}
