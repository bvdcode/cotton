// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Server.Auth;
using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Auth;
using Cotton.Server.Models.Dto;
using EasyExtensions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.IdentityModel.Tokens.Jwt;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route(Routes.V1.Auth)]
    public class AuthController(IMediator mediator) : ControllerBase
    {
        [Authorize]
        [HttpGet("webdav/token")]
        public async Task<IActionResult> GetWebDavToken(CancellationToken cancellationToken)
        {
            string token = await mediator.Send(new ResetWebDavTokenRequest(
                User.GetUserId(),
                Request.GetTrustedClientIPAddress(),
                Request.Headers.UserAgent.ToString()), cancellationToken);
            return Ok(token);
        }

        [Authorize]
        [HttpDelete("sessions/{sessionId}")]
        public async Task<IActionResult> RevokeSession(
            [FromRoute] string sessionId,
            CancellationToken cancellationToken)
        {
            await mediator.Send(new RevokeAuthSessionRequest(User.GetUserId(), sessionId), cancellationToken);
            return Ok();
        }

        [Authorize]
        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions(CancellationToken cancellationToken)
        {
            string sessionId = User.Claims.FirstOrDefault(x =>
                x.Type == JwtRegisteredClaimNames.Sid)?.Value ?? string.Empty;
            IEnumerable<SessionDto> sessions = await mediator.Send(
                new GetSessionsQuery(User.GetUserId(), sessionId), cancellationToken);
            return Ok(sessions);
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me(CancellationToken cancellationToken)
        {
            UserDto? user = await mediator.Send(new GetCurrentAuthUserQuery(User.GetUserId()), cancellationToken);
            return user is null ? this.ApiUnauthorized("User not found") : Ok(user);
        }

        [EnableRateLimiting(AuthRateLimitPolicies.Interactive)]
        [HttpPost("login")]
        public Task<ActionResult<AuthSessionResponseDto>> Login(
            LoginRequestDto request,
            CancellationToken cancellationToken)
        {
            return mediator.Send(new LoginWithPasswordRequest(
                request,
                Request.GetTrustedClientIPAddress(),
                Request.Headers.UserAgent.ToString()), cancellationToken);
        }

        [EnableRateLimiting(AuthRateLimitPolicies.Refresh)]
        [HttpPost("refresh")]
        public Task<ActionResult<AuthSessionResponseDto>> GetRefreshToken(
            [FromQuery] string? refreshToken = null)
        {
            return mediator.Send(new RefreshAuthSessionRequest(ResolveRefreshToken(refreshToken)),
                HttpContext.RequestAborted);
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromQuery] string? refreshToken = null)
        {
            await mediator.Send(new LogoutSessionRequest(ResolveRefreshToken(refreshToken)),
                HttpContext.RequestAborted);
            Response.Cookies.Delete(AuthConstants.RefreshTokenCookie);
            Response.Cookies.Delete(AuthConstants.AccessTokenCookie);
            return Ok();
        }

        [Authorize]
        [HttpPost("invalidate-share-links")]
        public async Task<IActionResult> InvalidateShareLinks(CancellationToken cancellationToken)
        {
            await mediator.Send(new InvalidateShareLinksRequest(User.GetUserId()), cancellationToken);
            return Ok();
        }

        private string? ResolveRefreshToken(string? refreshToken)
        {
            return string.IsNullOrEmpty(refreshToken)
                ? Request.Cookies[AuthConstants.RefreshTokenCookie]
                : refreshToken;
        }
    }
}
