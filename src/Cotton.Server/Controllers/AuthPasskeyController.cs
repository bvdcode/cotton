// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Auth;
using Cotton.Server.Handlers.Auth.Passkeys;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using EasyExtensions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route(Routes.V1.Auth)]
    public class AuthPasskeyController(IMediator mediator) : ControllerBase
    {
        [Authorize]
        [HttpGet("passkeys")]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            return Ok(await mediator.Send(new GetPasskeysQuery(User.GetUserId()), cancellationToken));
        }

        [Authorize]
        [HttpPost("passkeys/registration/options")]
        public async Task<IActionResult> BeginRegistration(
            [FromBody] BeginPasskeyRegistrationRequestDto request, CancellationToken cancellationToken)
        {
            return Ok(await mediator.Send(new BeginPasskeyRegistrationRequest(User.GetUserId(), request.Label), cancellationToken));
        }

        [Authorize]
        [HttpPost("passkeys/registration/verify")]
        public async Task<IActionResult> FinishRegistration(
            [FromBody] FinishPasskeyRegistrationRequestDto request, CancellationToken cancellationToken)
        {
            return Ok(await mediator.Send(new FinishPasskeyRegistrationRequest(User.GetUserId(), request), cancellationToken));
        }

        [Authorize]
        [HttpPut("passkeys/{credentialId:guid}")]
        public async Task<IActionResult> Rename(
            [FromRoute] Guid credentialId, [FromBody] RenamePasskeyRequestDto request, CancellationToken cancellationToken)
        {
            return Ok(await mediator.Send(new RenamePasskeyRequest(User.GetUserId(), credentialId, request.Label), cancellationToken));
        }

        [Authorize]
        [HttpDelete("passkeys/{credentialId:guid}")]
        public async Task<IActionResult> Delete([FromRoute] Guid credentialId, CancellationToken cancellationToken)
        {
            await mediator.Send(new DeletePasskeyRequest(User.GetUserId(), credentialId), cancellationToken);
            return Ok();
        }

        [EnableRateLimiting(AuthRateLimitPolicies.Interactive)]
        [HttpPost("passkeys/assertion/options")]
        public async Task<IActionResult> BeginAssertion(
            [FromBody] BeginPasskeyAssertionRequestDto request, CancellationToken cancellationToken)
        {
            return Ok(await mediator.Send(new BeginPasskeySignInRequest(request.Username), cancellationToken));
        }

        [EnableRateLimiting(AuthRateLimitPolicies.Interactive)]
        [HttpPost("passkeys/assertion/verify")]
        public Task<ActionResult<AuthSessionResponseDto>> FinishAssertion(
            [FromBody] FinishPasskeyAssertionRequestDto request, CancellationToken cancellationToken)
        {
            return mediator.Send(new FinishPasskeySignInRequest(request), cancellationToken);
        }
    }
}
