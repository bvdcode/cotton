// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Auth;
using Cotton.Server.Handlers.Users;
using Cotton.Server.Models.Requests;
using EasyExtensions;
using EasyExtensions.Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route(Routes.V1.Auth)]
    public class AuthRecoveryController(IMediator _mediator) : ControllerBase
    {
        [EnableRateLimiting(AuthRateLimitPolicies.Interactive)]
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(
            [FromBody] ForgotPasswordRequestDto request,
            CancellationToken cancellationToken)
        {
            SendPasswordResetRequest command = new(request.UsernameOrEmail, Request);
            await _mediator.Send(command, cancellationToken);
            return Ok();
        }

        [EnableRateLimiting(AuthRateLimitPolicies.Interactive)]
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(
            [FromBody] ResetPasswordRequestDto request,
            CancellationToken cancellationToken)
        {
            ConfirmPasswordResetRequest command = new(request.Token, request.NewPassword);
            await _mediator.Send(command, cancellationToken);
            return Ok();
        }
    }
}
