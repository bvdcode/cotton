// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Cotton.Database;
using Cotton.Database.Models;
using EasyExtensions.Helpers;
using Microsoft.AspNetCore.Mvc;
using EasyExtensions.Abstractions;
using EasyExtensions.AspNetCore.Authorization.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Controllers
{
    public record LoginRequest(string Username, string Password);
    public record LoginResponse(string AccessToken, string RefreshToken);
    public record RefreshTokenRequest(string RefreshToken);

    [ApiController]
    public class AuthController(
        ITokenProvider _tokens,
        CottonDbContext _dbContext,
        IPasswordHashService _hasher) : ControllerBase
    {
        private const int RefreshTokenLength = 64;

        [HttpPost("/api/v1/auth/login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            string normalizedUsername = request.Username.Trim().ToLowerInvariant();
            var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Username.ToLower() == normalizedUsername);
            if (user == null)
            {
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
                return Unauthorized();
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
            return Ok(new LoginResponse(accessToken, refreshToken));
        }

        [HttpPost("/api/v1/auth/refresh")]
        public async Task<IActionResult> GetRefreshToken(RefreshTokenRequest request)
        {
            var dbToken = await _dbContext.RefreshTokens.FindAsync(request.RefreshToken);
            if (dbToken == null || dbToken.RevokedAt != null)
            {
                return Unauthorized();
            }
            var user = await _dbContext.Users.FindAsync(dbToken.UserId);
            if (user == null)
            {
                return Unauthorized();
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
            return Ok(new LoginResponse(accessToken, newRefreshToken));
        }
    }
}
