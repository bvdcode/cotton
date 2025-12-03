// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Cotton.Database;
using Cotton.Server.Models;
using Cotton.Database.Models;
using EasyExtensions.Helpers;
using Microsoft.AspNetCore.Mvc;
using EasyExtensions.Abstractions;
using Microsoft.EntityFrameworkCore;
using Cotton.Server.Models.Requests;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.AspNetCore.Authorization.Abstractions;

namespace Cotton.Server.Controllers
{
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
            return Ok(new LoginResponse(accessToken, refreshToken));
        }

        [HttpPost("/api/v1/auth/refresh")]
        public async Task<IActionResult> GetRefreshToken(RefreshTokenRequest request)
        {
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
            return Ok(new LoginResponse(accessToken, newRefreshToken));
        }
    }
}
