// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Cotton.Shared;
using Cotton.Database;
using EasyExtensions.Models;
using EasyExtensions.Helpers;
using Microsoft.AspNetCore.Mvc;
using EasyExtensions.Abstractions;
using Microsoft.EntityFrameworkCore;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.EntityFrameworkCore.Database;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using EasyExtensions.AspNetCore.Authorization.Abstractions;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class AuthController(
        ITokenProvider _tokens,
        CottonSettings _settings,
        CottonDbContext _dbContext,
        IPasswordHashService _hasher) : ControllerBase
    {
        private const int RefreshTokenLength = 64;

        [HttpPost("/api/v1/auth/login")]
        public async Task<IActionResult> Login(LoginRequestDto request)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Username == request.Username);
            if (user == null)
            {
                // TODO: Return NotFound
                user = new()
                {
                    Username = request.Username.Trim(),
                    PasswordPhc = _hasher.Hash(request.Password)
                };
                await _dbContext.Users.AddAsync(user);
                await _dbContext.SaveChangesAsync();
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
            Response.Cookies.Append("refresh_token", refreshToken, new CookieOptions
            {
                Secure = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(_settings.SessionTimeoutHours)
            });
            return Ok(new TokenPairResponseDto()
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            });
        }

        [HttpPost("/api/v1/auth/refresh")]
        public async Task<IActionResult> GetRefreshToken(RefreshTokenRequestDto request)
        {
            // read from cookie if not provided in body
            if (string.IsNullOrEmpty(request.RefreshToken))
            {
                if (Request.Cookies.TryGetValue("refresh_token", out var cookieToken))
                {
                    request.RefreshToken = cookieToken;
                }
            }
            var dbToken = await _dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.Token == request.RefreshToken);
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
            Response.Cookies.Append("refresh_token", newRefreshToken, new CookieOptions
            {
                Secure = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(_settings.SessionTimeoutHours)
            });
            return Ok(new TokenPairResponseDto()
            {
                AccessToken = accessToken,
                RefreshToken = newRefreshToken
            });
        }

        [HttpPost("/api/v1/auth/logout")]
        public async Task<IActionResult> Logout(RefreshTokenRequestDto request)
        {
            // read from cookie if not provided in body
            if (string.IsNullOrEmpty(request.RefreshToken))
            {
                if (Request.Cookies.TryGetValue("refresh_token", out var cookieToken))
                {
                    request.RefreshToken = cookieToken;
                }
            }
            var dbToken = await _dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.Token == request.RefreshToken);
            if (dbToken != null && dbToken.RevokedAt == null)
            {
                dbToken.RevokedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
            }
            Response.Cookies.Delete("refresh_token");
            return Ok();
        }
    }
}
