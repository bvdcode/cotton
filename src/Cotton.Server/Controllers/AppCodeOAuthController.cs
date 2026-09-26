// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton;
using Cotton.Auth;
using Cotton.Server.Auth;
using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Auth.AppCode;
using Cotton.Server.Services;
using EasyExtensions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Net;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route(Routes.V1.AppCodeOAuth)]
    public class AppCodeOAuthController(IMediator mediator) : ControllerBase
    {
        private const int PollIntervalSeconds = 2;

        [AllowAnonymous]
        [EnableRateLimiting(AuthRateLimitPolicies.Interactive)]
        [HttpPost("start")]
        public async Task<IActionResult> Start(
            [FromBody] AppCodeStartRequestDto request,
            CancellationToken cancellationToken)
        {
            IPAddress originAddress = Request.GetTrustedClientIPAddress();
            AppCodeStartResponseDto? result = await mediator.Send(new StartAppCodeRequest(
                request, originAddress, Request.Headers.UserAgent.ToString()), cancellationToken);
            return result is null
                ? StatusCode(StatusCodes.Status429TooManyRequests, new AppCodePollErrorDto
                {
                    Error = "too_many_requests",
                    RetryAfterSeconds = PollIntervalSeconds,
                })
                : Ok(result);
        }

        [Authorize]
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            AppCodeDetailsDto result = await mediator.Send(new GetAppCodeRequest(id), cancellationToken);
            return Ok(result);
        }

        [Authorize]
        [HttpPost("{id:guid}/approve")]
        public async Task<IActionResult> Approve([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            await mediator.Send(new ApproveAppCodeRequest(id, User.GetUserId()), cancellationToken);
            return Ok();
        }

        [Authorize]
        [HttpPost("{id:guid}/deny")]
        public async Task<IActionResult> Deny([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            await mediator.Send(new DenyAppCodeRequest(id), cancellationToken);
            return Ok();
        }

        [AllowAnonymous]
        [EnableRateLimiting(AuthRateLimitPolicies.Refresh)]
        [HttpPost("poll")]
        public async Task<IActionResult> Poll(
            [FromBody] AppCodePollRequestDto request,
            CancellationToken cancellationToken)
        {
            AppCodePollResult result = await mediator.Send(new PollAppCodeRequest(request.PollToken), cancellationToken);
            return result.Status switch
            {
                AppCodePollStatus.NotFound => NotFound(new AppCodePollErrorDto { Error = "not_found" }),
                AppCodePollStatus.Expired => StatusCode(StatusCodes.Status410Gone,
                    new AppCodePollErrorDto { Error = "expired" }),
                AppCodePollStatus.Denied => StatusCode(StatusCodes.Status403Forbidden,
                    new AppCodePollErrorDto { Error = "denied" }),
                AppCodePollStatus.Approved => Ok(result.Tokens),
                AppCodePollStatus.Pending => Accepted(new AppCodePollErrorDto
                {
                    Error = "pending",
                    RetryAfterSeconds = PollIntervalSeconds,
                }),
                _ => throw new InvalidOperationException($"Unsupported app-code poll status: {result.Status}"),
            };
        }
    }
}
