// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Auth;
using Cotton.Server.Extensions;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Server.Services;
using EasyExtensions;
using EasyExtensions.AspNetCore.Authorization.Abstractions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route(Routes.V1.Auth)]
    public class AuthPasskeyController(
        PasskeyService _passkeys,
        AuthSessionIssuer _sessionIssuer) : ControllerBase
    {
        [Authorize]
        [HttpGet("passkeys")]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            Guid userId = User.GetUserId();
            IReadOnlyList<PasskeyCredentialDto> credentials = await _passkeys.GetCredentialsAsync(
                userId,
                cancellationToken);
            return Ok(credentials);
        }

        [Authorize]
        [HttpPost("passkeys/registration/options")]
        public async Task<IActionResult> BeginRegistration(
            [FromBody] BeginPasskeyRegistrationRequestDto request,
            CancellationToken cancellationToken)
        {
            PasskeyRegistrationOptionsResponseDto response = await _passkeys.BeginRegistrationAsync(
                User.GetUserId(),
                request.Label,
                cancellationToken);
            return Ok(response);
        }

        [Authorize]
        [HttpPost("passkeys/registration/verify")]
        public async Task<IActionResult> FinishRegistration(
            [FromBody] FinishPasskeyRegistrationRequestDto request,
            CancellationToken cancellationToken)
        {
            PasskeyCredentialDto response = await _passkeys.FinishRegistrationAsync(
                User.GetUserId(),
                request,
                cancellationToken);
            return Ok(response);
        }

        [Authorize]
        [HttpPut("passkeys/{credentialId:guid}")]
        public async Task<IActionResult> Rename(
            [FromRoute] Guid credentialId,
            [FromBody] RenamePasskeyRequestDto request,
            CancellationToken cancellationToken)
        {
            PasskeyCredentialDto response = await _passkeys.SetCredentialLabelAsync(
                User.GetUserId(),
                credentialId,
                request.Label,
                cancellationToken);
            return Ok(response);
        }

        [Authorize]
        [HttpDelete("passkeys/{credentialId:guid}")]
        public async Task<IActionResult> Delete(
            [FromRoute] Guid credentialId,
            CancellationToken cancellationToken)
        {
            await _passkeys.DeleteCredentialAsync(User.GetUserId(), credentialId, cancellationToken);
            return Ok();
        }

        [EnableRateLimiting(AuthRateLimitPolicies.Interactive)]
        [HttpPost("passkeys/assertion/options")]
        public async Task<IActionResult> BeginAssertion(
            [FromBody] BeginPasskeyAssertionRequestDto request,
            CancellationToken cancellationToken)
        {
            PasskeyAssertionOptionsResponseDto response = await _passkeys.BeginAssertionAsync(
                request.Username,
                cancellationToken);
            return Ok(response);
        }

        [EnableRateLimiting(AuthRateLimitPolicies.Interactive)]
        [HttpPost("passkeys/assertion/verify")]
        public async Task<IActionResult> FinishAssertion(
            [FromBody] FinishPasskeyAssertionRequestDto request,
            CancellationToken cancellationToken)
        {
            try
            {
                User user = await _passkeys.FinishAssertionAsync(request, cancellationToken);
                return Ok(await _sessionIssuer.SignInAsync(
                    user,
                    request.TrustDevice,
                    AuthType.Passkey,
                    cancellationToken));
            }
            catch (UnauthorizedAccessException)
            {
                return this.ApiUnauthorized("Invalid passkey");
            }
        }
    }
}
