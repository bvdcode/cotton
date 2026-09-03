// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Auth;
using Cotton.Server.Models.Requests;
using EasyExtensions;
using EasyExtensions.AspNetCore.Authorization.Abstractions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Authorize]
    [Route(Routes.V1.Auth)]
    public class AuthTotpController(IMediator _mediator) : ControllerBase
    {
        [HttpDelete("totp/disable")]
        public async Task<IActionResult> Disable(
            [FromBody] DisableTotpRequestDto request,
            CancellationToken cancellationToken)
        {
            DisableTotpRequest command = new(
                User.GetUserId(),
                request.Password,
                GetRequestIpAddress(),
                Request.Headers.UserAgent.ToString());
            TotpOperationResult result = await _mediator.Send(command, cancellationToken);
            return ToActionResult(result);
        }

        [HttpPost("totp/confirm")]
        public async Task<IActionResult> Confirm(
            [FromBody] ConfirmTotpRequestDto request,
            CancellationToken cancellationToken)
        {
            ConfirmTotpRequest command = new(
                User.GetUserId(),
                request.TwoFactorCode,
                GetRequestIpAddress(),
                Request.Headers.UserAgent.ToString());
            TotpOperationResult result = await _mediator.Send(command, cancellationToken);
            return ToActionResult(result);
        }

        [HttpPost("totp/setup")]
        public async Task<IActionResult> Setup(CancellationToken cancellationToken)
        {
            SetupTotpRequest command = new(User.GetUserId(), Request.Host.Host);
            TotpOperationResult result = await _mediator.Send(command, cancellationToken);
            return ToActionResult(result);
        }

        private IActionResult ToActionResult(TotpOperationResult result)
        {
            return result.Status switch
            {
                TotpOperationStatus.Success when result.Setup is not null => Ok(result.Setup),
                TotpOperationStatus.Success => Ok(),
                TotpOperationStatus.BadRequest => this.ApiBadRequest(result.Error!),
                TotpOperationStatus.Unauthorized => this.ApiUnauthorized(result.Error!),
                TotpOperationStatus.Forbidden => this.ApiForbidden(result.Error!),
                TotpOperationStatus.Conflict => this.ApiConflict(result.Error!),
                _ => throw new InvalidOperationException($"Unsupported TOTP operation status: {result.Status}"),
            };
        }

        private IPAddress GetRequestIpAddress()
        {
            return Constants.IsPublicInstance
                ? IPAddress.Loopback
                : Request.GetTrustedClientIPAddress();
        }
    }
}
