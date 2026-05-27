// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Auth;
using Cotton.Server.Extensions;
using Cotton.Server.Models.Dto;
using EasyExtensions.AspNetCore.Extensions;
using Cotton.Server.Models.Requests;
using Cotton.Server.Services;
using EasyExtensions;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cotton.Server.Controllers;

/// <summary>Exposes OpenID Connect provider and sign-in endpoints.</summary>
[ApiController]
[Route(Routes.V1.Auth + "/oidc")]
public sealed class OidcController(
    OidcProviderService _providers,
    OidcAuthenticationService _auth) : ControllerBase
{
    /// <summary>Lists enabled providers for the login page.</summary>
    [HttpGet("providers")]
    public async Task<IActionResult> GetPublicProviders(CancellationToken cancellationToken)
    {
        return Ok(await _providers.ListPublicAsync(cancellationToken));
    }

    /// <summary>Lists all providers for administrators.</summary>
    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpGet("providers/admin")]
    public async Task<IActionResult> GetAdminProviders(CancellationToken cancellationToken)
    {
        return Ok(await _providers.ListAdminAsync(cancellationToken));
    }

    /// <summary>Creates a provider.</summary>
    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPost("providers")]
    public async Task<IActionResult> CreateProvider(
        [FromBody] OidcProviderRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _providers.CreateAsync(request, cancellationToken));
    }

    /// <summary>Updates a provider.</summary>
    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPut("providers/{providerId:guid}")]
    public async Task<IActionResult> UpdateProvider(
        [FromRoute] Guid providerId,
        [FromBody] OidcProviderRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _providers.UpdateAsync(providerId, request, cancellationToken));
    }

    /// <summary>Deletes a provider.</summary>
    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpDelete("providers/{providerId:guid}")]
    public async Task<IActionResult> DeleteProvider(
        [FromRoute] Guid providerId,
        CancellationToken cancellationToken)
    {
        await _providers.DeleteAsync(providerId, cancellationToken);
        return NoContent();
    }

    /// <summary>Creates a sign-in authorization URL for a provider.</summary>
    [EnableRateLimiting(AuthRateLimitPolicies.Interactive)]
    [HttpPost("start/{providerSlug}/authorization-url")]
    public async Task<ActionResult<OidcAuthorizationUrlDto>> CreateSignInAuthorizationUrl(
        [FromRoute] string providerSlug,
        [FromBody] OidcAuthorizationRequestDto? request,
        CancellationToken cancellationToken)
    {
        string authorizationUrl = await _auth.BeginSignInAsync(
            providerSlug,
            request?.ReturnUrl,
            request?.TrustDevice ?? false,
            cancellationToken);
        return Ok(new OidcAuthorizationUrlDto { AuthorizationUrl = authorizationUrl });
    }

    /// <summary>Creates an account-linking authorization URL for a provider.</summary>
    [Authorize]
    [EnableRateLimiting(AuthRateLimitPolicies.Interactive)]
    [HttpPost("link/{providerSlug}/authorization-url")]
    public async Task<ActionResult<OidcAuthorizationUrlDto>> CreateLinkAuthorizationUrl(
        [FromRoute] string providerSlug,
        [FromBody] OidcAuthorizationRequestDto? request,
        CancellationToken cancellationToken)
    {
        string authorizationUrl = await _auth.BeginLinkAsync(
            User.GetUserId(),
            providerSlug,
            request?.ReturnUrl,
            cancellationToken);
        return Ok(new OidcAuthorizationUrlDto { AuthorizationUrl = authorizationUrl });
    }

    /// <summary>Completes an OIDC authorization-code callback.</summary>
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

        string returnUrl = await _auth.CompleteCallbackAsync(
            state.Trim(),
            code.Trim(),
            cancellationToken);
        return Redirect(returnUrl);
    }

    /// <summary>Lists provider links for the current user.</summary>
    [Authorize]
    [HttpGet("links")]
    public async Task<IActionResult> GetLinks(CancellationToken cancellationToken)
    {
        return Ok(await _auth.ListLinkedAsync(User.GetUserId(), cancellationToken));
    }

    /// <summary>Unlinks a provider from the current user.</summary>
    [Authorize]
    [HttpDelete("links/{identityId:guid}")]
    public async Task<IActionResult> Unlink(
        [FromRoute] Guid identityId,
        CancellationToken cancellationToken)
    {
        await _auth.UnlinkAsync(User.GetUserId(), identityId, cancellationToken);
        return NoContent();
    }
}
