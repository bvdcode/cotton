// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Auth;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Server.Handlers.Auth.Oidc;
using EasyExtensions.Mediator;
using EasyExtensions;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route(Routes.V1.Auth + "/oidc")]
    public class OidcController(IMediator mediator) : ControllerBase
    {
        [HttpGet("providers")]
        public async Task<IActionResult> GetPublicProviders(CancellationToken cancellationToken)
        {
            return Ok(await mediator.Send(new GetPublicOidcProvidersQuery(), cancellationToken));
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("providers/admin")]
        public async Task<IActionResult> GetAdminProviders(CancellationToken cancellationToken)
        {
            return Ok(await mediator.Send(new GetAdminOidcProvidersQuery(), cancellationToken));
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPost("providers")]
        public async Task<IActionResult> CreateProvider(
            [FromBody] OidcProviderRequestDto request,
            CancellationToken cancellationToken)
        {
            return Ok(await mediator.Send(new CreateOidcProviderRequest(request), cancellationToken));
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPut("providers/{providerId:guid}")]
        public async Task<IActionResult> UpdateProvider(
            [FromRoute] Guid providerId,
            [FromBody] OidcProviderRequestDto request,
            CancellationToken cancellationToken)
        {
            return Ok(await mediator.Send(new UpdateOidcProviderRequest(providerId, request), cancellationToken));
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpDelete("providers/{providerId:guid}")]
        public async Task<IActionResult> DeleteProvider(
            [FromRoute] Guid providerId,
            CancellationToken cancellationToken)
        {
            await mediator.Send(new DeleteOidcProviderRequest(providerId), cancellationToken);
            return NoContent();
        }

        [EnableRateLimiting(AuthRateLimitPolicies.Interactive)]
        [HttpPost("start/{providerSlug}/authorization-url")]
        public async Task<ActionResult<OidcAuthorizationUrlDto>> CreateSignInAuthorizationUrl(
            [FromRoute] string providerSlug,
            [FromBody] OidcAuthorizationRequestDto? request,
            CancellationToken cancellationToken)
        {
            string authorizationUrl = await mediator.Send(new BeginOidcAuthenticationRequest(
                providerSlug, request?.ReturnUrl, request?.TrustDevice ?? false), cancellationToken);
            return Ok(new OidcAuthorizationUrlDto { AuthorizationUrl = authorizationUrl });
        }

        [Authorize]
        [EnableRateLimiting(AuthRateLimitPolicies.Interactive)]
        [HttpPost("link/{providerSlug}/authorization-url")]
        public async Task<ActionResult<OidcAuthorizationUrlDto>> CreateLinkAuthorizationUrl(
            [FromRoute] string providerSlug,
            [FromBody] OidcAuthorizationRequestDto? request,
            CancellationToken cancellationToken)
        {
            string authorizationUrl = await mediator.Send(new BeginOidcAuthenticationRequest(
                providerSlug, request?.ReturnUrl, false, User.GetUserId()), cancellationToken);
            return Ok(new OidcAuthorizationUrlDto { AuthorizationUrl = authorizationUrl });
        }

        [EnableRateLimiting(AuthRateLimitPolicies.Interactive)]
        [HttpGet("callback")]
        public async Task<IActionResult> Callback(
            [FromQuery] string? state,
            [FromQuery] string? code,
            [FromQuery] string? error,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                return Redirect("/login?oidc=cancelled");
            }

            if (string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(code))
            {
                return BadRequest("OIDC callback is missing state or code.");
            }

            string returnUrl = await mediator.Send(new CompleteOidcAuthenticationRequest(state.Trim(), code.Trim()), cancellationToken);
            return Redirect(returnUrl);
        }

        [Authorize]
        [HttpGet("links")]
        public async Task<IActionResult> GetLinks(CancellationToken cancellationToken)
        {
            return Ok(await mediator.Send(new GetLinkedOidcIdentitiesQuery(User.GetUserId()), cancellationToken));
        }

        [Authorize]
        [HttpDelete("links/{identityId:guid}")]
        public async Task<IActionResult> Unlink(
            [FromRoute] Guid identityId,
            CancellationToken cancellationToken)
        {
            await mediator.Send(new UnlinkOidcIdentityRequest(User.GetUserId(), identityId), cancellationToken);
            return NoContent();
        }
    }
}
