// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using Cotton.Server.Models;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.EntityFrameworkCore.Database;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.Net;

namespace Cotton.Server.Controllers;

/// <summary>
/// Exposes in-memory app-code authorization endpoints for desktop and native applications.
/// </summary>
[ApiController]
[Route("/api/v1/oauth/app-code")]
public sealed class AppCodeOAuthController(
    CottonDbContext _dbContext,
    AuthSessionIssuer _sessionIssuer,
    INotificationsProvider _notifications,
    IDatabaseIntegrityVerifier _integrity,
    IGeoLookupService _geoLookup,
    ILogger<AppCodeOAuthController> _logger) : ControllerBase
{
    private static readonly ConcurrentDictionary<Guid, AppCodeRequestState> Requests = new();
    private static readonly TimeSpan RequestLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LongPollTimeout = TimeSpan.FromSeconds(25);

    /// <summary>
    /// Starts an app-code authorization request.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("start")]
    public ActionResult<AppCodeStartResponseDto> Start([FromBody] AppCodeStartRequestDto request)
    {
        CleanupExpiredRequests();
        string applicationName = NormalizeRequired(request.ApplicationName, "ApplicationName", maxLength: 120);
        string applicationVersion = NormalizeOptional(request.ApplicationVersion, maxLength: 80) ?? "Unknown version";
        string? deviceName = NormalizeOptional(request.DeviceName, maxLength: 160);
        Guid id = Guid.NewGuid();
        DateTime now = DateTime.UtcNow;
        DateTime expiresAt = now.Add(RequestLifetime);
        IPAddress originAddress = ResolveRequestIpAddress(Request);
        string origin = originAddress.ToString();
        string userAgent = Request.Headers.UserAgent.ToString();
        var state = new AppCodeRequestState(
            id,
            applicationName,
            applicationVersion,
            deviceName,
            origin,
            userAgent,
            now,
            expiresAt);
        Requests[id] = state;

        return Ok(new AppCodeStartResponseDto
        {
            Id = id,
            ApprovalUrl = $"/oauth/app-code/{id:D}",
            ExpiresAt = expiresAt,
            PollIntervalSeconds = (int)PollInterval.TotalSeconds,
        });
    }

    /// <summary>
    /// Gets details for the browser approval screen.
    /// </summary>
    [Authorize]
    [HttpGet("{id:guid}")]
    public ActionResult<AppCodeDetailsDto> Get([FromRoute] Guid id)
    {
        CleanupExpiredRequests();
        AppCodeRequestState state = GetExistingState(id);
        return Ok(new AppCodeDetailsDto
        {
            Id = state.Id,
            ApplicationName = state.ApplicationName,
            ApplicationVersion = state.ApplicationVersion,
            DeviceName = state.DeviceName,
            Origin = state.Origin,
            RequestedAt = state.RequestedAt,
            ExpiresAt = state.ExpiresAt,
            Status = state.Status.ToString().ToLowerInvariant(),
        });
    }

    /// <summary>
    /// Approves an app-code request for the current authenticated user.
    /// </summary>
    [Authorize]
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        CleanupExpiredRequests();
        AppCodeRequestState state = GetExistingState(id);

        await state.Gate.WaitAsync(cancellationToken);
        try
        {
            EnsurePending(state);
            Guid userId = User.GetUserId();
            User user = await _dbContext.Users.FindAsync([userId], cancellationToken)
                ?? throw new EntityNotFoundException<User>();
            _integrity.RequireValid(_dbContext, user, "oauth.app-code.approve-user");

            var (dbToken, refreshToken) = await _sessionIssuer
                .CreateRefreshTokenAsync(user, trustDevice: true, AuthType.Credentials)
                .ConfigureAwait(false);
            await ApplyApplicationSessionMetadataAsync(dbToken, state, cancellationToken);

            string accessToken = _sessionIssuer.CreateAccessToken(user, dbToken.SessionId!);
            await _dbContext.RefreshTokens.AddAsync(dbToken, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            state.Tokens = new TokenPairResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
            };
            state.Status = AppCodeRequestStatus.Approved;
            state.ApprovedAt = DateTime.UtcNow;
            state.ApprovedUserId = userId;

            await SendApprovedNotificationAsync(userId, state);
            state.Completion.TrySetResult();
        }
        finally
        {
            state.Gate.Release();
        }

        return Ok();
    }

    /// <summary>
    /// Denies an app-code request.
    /// </summary>
    [Authorize]
    [HttpPost("{id:guid}/deny")]
    public async Task<IActionResult> Deny([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        CleanupExpiredRequests();
        AppCodeRequestState state = GetExistingState(id);

        await state.Gate.WaitAsync(cancellationToken);
        try
        {
            EnsurePending(state);
            state.Status = AppCodeRequestStatus.Denied;
            state.Completion.TrySetResult();
        }
        finally
        {
            state.Gate.Release();
        }

        return Ok();
    }

    /// <summary>
    /// Polls an app-code request until approval returns a token pair.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("{id:guid}/poll")]
    public async Task<IActionResult> Poll([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        CleanupExpiredRequests();
        if (!Requests.TryGetValue(id, out AppCodeRequestState? state))
        {
            return NotFound(new AppCodePollErrorDto { Error = "not_found" });
        }

        if (state.Status == AppCodeRequestStatus.Pending && !IsExpired(state))
        {
            TimeSpan wait = GetPollWaitTimeout(state);
            if (wait > TimeSpan.Zero)
            {
                await Task.WhenAny(state.Completion.Task, Task.Delay(wait, cancellationToken));
            }
        }

        await state.Gate.WaitAsync(cancellationToken);
        try
        {
            if (IsExpired(state))
            {
                Requests.TryRemove(id, out _);
                return StatusCode(StatusCodes.Status410Gone, new AppCodePollErrorDto { Error = "expired" });
            }

            if (state.Status == AppCodeRequestStatus.Denied)
            {
                Requests.TryRemove(id, out _);
                return StatusCode(StatusCodes.Status403Forbidden, new AppCodePollErrorDto { Error = "denied" });
            }

            if (state.Status == AppCodeRequestStatus.Approved && state.Tokens is not null)
            {
                TokenPairResponseDto tokens = state.Tokens;
                state.Status = AppCodeRequestStatus.Consumed;
                Requests.TryRemove(id, out _);
                return Ok(tokens);
            }

            return Accepted(new AppCodePollErrorDto
            {
                Error = "pending",
                RetryAfterSeconds = (int)PollInterval.TotalSeconds,
            });
        }
        finally
        {
            state.Gate.Release();
        }
    }

    private async Task ApplyApplicationSessionMetadataAsync(
        ExtendedRefreshToken dbToken,
        AppCodeRequestState state,
        CancellationToken cancellationToken)
    {
        dbToken.Device = BuildSessionDeviceName(state);
        dbToken.UserAgent = state.UserAgent;
        if (!IPAddress.TryParse(state.Origin, out IPAddress? originAddress))
        {
            return;
        }

        dbToken.IpAddress = originAddress;
        GeoLookupResult? lookup = await _geoLookup.TryLookupAsync(originAddress, cancellationToken);
        dbToken.Country = NormalizeGeoField(lookup?.Country);
        dbToken.Region = NormalizeGeoField(lookup?.Region);
        dbToken.City = NormalizeGeoField(lookup?.City);
    }

    private async Task SendApprovedNotificationAsync(Guid userId, AppCodeRequestState state)
    {
        try
        {
            await _notifications.SendNotificationAsync(
                userId,
                "Application sign-in approved",
                $"{state.ApplicationName} {state.ApplicationVersion} signed in from {state.Origin}.",
                NotificationPriority.Medium,
                new Dictionary<string, string>
                {
                    ["applicationName"] = state.ApplicationName,
                    ["applicationVersion"] = state.ApplicationVersion,
                    ["origin"] = state.Origin,
                    ["requestId"] = state.Id.ToString("D"),
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send app-code approval notification for request {RequestId}",
                state.Id);
        }
    }

    private static AppCodeRequestState GetExistingState(Guid id)
    {
        if (!Requests.TryGetValue(id, out AppCodeRequestState? state) || IsExpired(state))
        {
            Requests.TryRemove(id, out _);
            throw new EntityNotFoundException<AppCodeDetailsDto>();
        }

        return state;
    }

    private static void EnsurePending(AppCodeRequestState state)
    {
        if (IsExpired(state))
        {
            Requests.TryRemove(state.Id, out _);
            throw new BadRequestException<AppCodeDetailsDto>("Application sign-in request has expired.");
        }

        if (state.Status != AppCodeRequestStatus.Pending)
        {
            throw new BadRequestException<AppCodeDetailsDto>("Application sign-in request is no longer pending.");
        }
    }

    private static void CleanupExpiredRequests()
    {
        foreach ((Guid id, AppCodeRequestState state) in Requests)
        {
            if (IsExpired(state))
            {
                state.Completion.TrySetResult();
                Requests.TryRemove(id, out _);
            }
        }
    }

    private static bool IsExpired(AppCodeRequestState state)
    {
        return state.ExpiresAt <= DateTime.UtcNow;
    }

    private static TimeSpan GetPollWaitTimeout(AppCodeRequestState state)
    {
        TimeSpan remaining = state.ExpiresAt - DateTime.UtcNow;
        return remaining <= LongPollTimeout ? remaining : LongPollTimeout;
    }

    private static string BuildSessionDeviceName(AppCodeRequestState state)
    {
        string app = state.ApplicationVersion == "Unknown version"
            ? state.ApplicationName
            : $"{state.ApplicationName} {state.ApplicationVersion}";
        return string.IsNullOrWhiteSpace(state.DeviceName)
            ? app
            : $"{app} on {state.DeviceName}";
    }

    private static string NormalizeRequired(string? value, string fieldName, int maxLength)
    {
        string? normalized = NormalizeOptional(value, maxLength);
        if (normalized is null)
        {
            throw new BadRequestException<AppCodeStartRequestDto>($"{fieldName} is required.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        string? normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static IPAddress ResolveRequestIpAddress(HttpRequest request)
    {
        return Constants.IsPublicInstance
            ? IPAddress.Loopback
            : request.GetRemoteIPAddress();
    }

    private static string NormalizeGeoField(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
    }

    private sealed class AppCodeRequestState
    {
        public AppCodeRequestState(
            Guid id,
            string applicationName,
            string applicationVersion,
            string? deviceName,
            string origin,
            string userAgent,
            DateTime requestedAt,
            DateTime expiresAt)
        {
            Id = id;
            ApplicationName = applicationName;
            ApplicationVersion = applicationVersion;
            DeviceName = deviceName;
            Origin = origin;
            UserAgent = userAgent;
            RequestedAt = requestedAt;
            ExpiresAt = expiresAt;
        }

        public Guid Id { get; }

        public string ApplicationName { get; }

        public string ApplicationVersion { get; }

        public string? DeviceName { get; }

        public string Origin { get; }

        public string UserAgent { get; }

        public DateTime RequestedAt { get; }

        public DateTime ExpiresAt { get; }

        public SemaphoreSlim Gate { get; } = new(1, 1);

        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public AppCodeRequestStatus Status { get; set; }

        public Guid? ApprovedUserId { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public TokenPairResponseDto? Tokens { get; set; }
    }

    private enum AppCodeRequestStatus
    {
        Pending,
        Approved,
        Denied,
        Consumed,
    }
}

/// <summary>
/// Request payload for starting an app-code authorization request.
/// </summary>
public sealed class AppCodeStartRequestDto
{
    /// <summary>
    /// Name of the requesting application.
    /// </summary>
    public string ApplicationName { get; set; } = null!;

    /// <summary>
    /// Version of the requesting application.
    /// </summary>
    public string? ApplicationVersion { get; set; }

    /// <summary>
    /// Optional device name shown in session history.
    /// </summary>
    public string? DeviceName { get; set; }
}

/// <summary>
/// Response payload returned after starting an app-code authorization request.
/// </summary>
public sealed class AppCodeStartResponseDto
{
    /// <summary>
    /// Authorization request id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Browser path where the user can approve the request.
    /// </summary>
    public string ApprovalUrl { get; set; } = null!;

    /// <summary>
    /// UTC timestamp when the request expires.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Suggested polling interval in seconds.
    /// </summary>
    public int PollIntervalSeconds { get; set; }
}

/// <summary>
/// Browser-facing details for an app-code authorization request.
/// </summary>
public sealed class AppCodeDetailsDto
{
    /// <summary>
    /// Authorization request id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Name of the requesting application.
    /// </summary>
    public string ApplicationName { get; set; } = null!;

    /// <summary>
    /// Version of the requesting application.
    /// </summary>
    public string ApplicationVersion { get; set; } = null!;

    /// <summary>
    /// Optional device name supplied by the requesting application.
    /// </summary>
    public string? DeviceName { get; set; }

    /// <summary>
    /// Origin address of the request.
    /// </summary>
    public string Origin { get; set; } = null!;

    /// <summary>
    /// UTC timestamp when the request was created.
    /// </summary>
    public DateTime RequestedAt { get; set; }

    /// <summary>
    /// UTC timestamp when the request expires.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Current request status.
    /// </summary>
    public string Status { get; set; } = null!;

}

/// <summary>
/// Polling error payload for app-code authorization requests.
/// </summary>
public sealed class AppCodePollErrorDto
{
    /// <summary>
    /// Machine-readable polling error.
    /// </summary>
    public string Error { get; set; } = null!;

    /// <summary>
    /// Optional retry interval in seconds.
    /// </summary>
    public int? RetryAfterSeconds { get; set; }
}
