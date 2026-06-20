// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Localization;
using Cotton.Auth;
using Cotton;
using Cotton.Server.Auth;
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
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Cotton.Server.Controllers
{

/// <summary>
/// Exposes in-memory app-code authorization endpoints for desktop and native applications.
/// </summary>
[ApiController]
[Route(Routes.V1.AppCodeOAuth)]
public class AppCodeOAuthController(
    CottonDbContext _dbContext,
    AuthSessionIssuer _sessionIssuer,
    INotificationsProvider _notifications,
    IDatabaseIntegrityVerifier _integrity,
    IGeoLookupService _geoLookup,
    ILogger<AppCodeOAuthController> _logger) : ControllerBase
{
    private const int MaxActiveRequests = 1024;
    private const int PollSecretByteLength = 32;
    private const int PollSecretLength = PollSecretByteLength * 2;

    private static readonly Lock RequestsGate = new();
    private static readonly MemoryCache Requests = new(new MemoryCacheOptions
    {
        SizeLimit = MaxActiveRequests,
        ExpirationScanFrequency = TimeSpan.FromSeconds(30),
    });
    private static readonly TimeSpan RequestLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LongPollTimeout = TimeSpan.FromSeconds(25);

    /// <summary>
    /// Starts an app-code authorization request.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimitPolicies.Interactive)]
    [HttpPost("start")]
    public ActionResult<AppCodeStartResponseDto> Start([FromBody] AppCodeStartRequestDto request)
    {
        string applicationName = NormalizeRequired(request.ApplicationName, "ApplicationName", maxLength: 120);
        string applicationVersion = NormalizeOptional(request.ApplicationVersion, maxLength: 80) ?? "Unknown version";
        string? deviceName = NormalizeOptional(request.DeviceName, maxLength: 160);
        Guid approvalId = Guid.NewGuid();
        DateTime now = DateTime.UtcNow;
        DateTime expiresAt = now.Add(RequestLifetime);
        IPAddress originAddress = ResolveRequestIpAddress(Request);
        string origin = originAddress.ToString();
        string userAgent = Request.Headers.UserAgent.ToString();

        string pollSecret = CreatePollSecret();
        string pollToken = CreatePollToken(approvalId, pollSecret);
        lock (RequestsGate)
        {
            if (Requests.Count >= MaxActiveRequests)
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new AppCodePollErrorDto
                {
                    Error = "too_many_requests",
                    RetryAfterSeconds = (int)PollInterval.TotalSeconds,
                });
            }

            var state = new AppCodeRequestState(
                approvalId,
                HashPollSecret(pollSecret),
                applicationName,
                applicationVersion,
                deviceName,
                origin,
                userAgent,
                now,
                expiresAt);
            Requests.Set(GetCacheKey(approvalId), state, CreateCacheEntryOptions(expiresAt));
        }

        return Ok(new AppCodeStartResponseDto
        {
            ApprovalId = approvalId,
            ApprovalUrl = $"/oauth/app-code/{approvalId:D}",
            PollToken = pollToken,
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
        AppCodeRequestState state = GetExistingState(id);
        return Ok(new AppCodeDetailsDto
        {
            Id = state.ApprovalId,
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
    [EnableRateLimiting(AuthRateLimitPolicies.Refresh)]
    [HttpPost("poll")]
    public async Task<IActionResult> Poll([FromBody] AppCodePollRequestDto request, CancellationToken cancellationToken)
    {
        (Guid approvalId, string pollSecret) = ParsePollToken(request.PollToken);
        if (!Requests.TryGetValue(GetCacheKey(approvalId), out AppCodeRequestState? cachedState)
            || cachedState is null
            || !IsPollSecretValid(cachedState, pollSecret))
        {
            return NotFound(new AppCodePollErrorDto { Error = "not_found" });
        }

        AppCodeRequestState state = cachedState;
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
                RemoveRequest(state);
                return StatusCode(StatusCodes.Status410Gone, new AppCodePollErrorDto { Error = "expired" });
            }

            if (state.Status == AppCodeRequestStatus.Denied)
            {
                RemoveRequest(state);
                return StatusCode(StatusCodes.Status403Forbidden, new AppCodePollErrorDto { Error = "denied" });
            }

            if (state.Status == AppCodeRequestStatus.Approved && state.Tokens is not null)
            {
                TokenPairResponseDto tokens = state.Tokens;
                state.Status = AppCodeRequestStatus.Consumed;
                state.Tokens = null;
                RemoveRequest(state);
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
            var metadata = new Dictionary<string, string>
            {
                ["applicationName"] = state.ApplicationName,
                ["applicationVersion"] = state.ApplicationVersion,
                ["origin"] = state.Origin,
                ["requestId"] = state.ApprovalId.ToString("D"),
            };
            Dictionary<string, string> templateMetadata = NotificationTemplateMetadata.Create(
                NotificationTemplateKeys.AppCodeApprovalTitle,
                NotificationTemplateKeys.AppCodeApprovalContent,
                metadata);

            await _notifications.SendNotificationAsync(
                userId,
                NotificationTemplates.AppCodeApprovalTitle,
                NotificationTemplates.AppCodeApprovalContent(
                    state.ApplicationName,
                    state.ApplicationVersion,
                    state.Origin),
                NotificationPriority.Medium,
                templateMetadata);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send app-code approval notification for request {RequestId}",
                state.ApprovalId);
        }
    }

    private static AppCodeRequestState GetExistingState(Guid id)
    {
        if (!Requests.TryGetValue(GetCacheKey(id), out AppCodeRequestState? state) || state is null)
        {
            throw new EntityNotFoundException<AppCodeDetailsDto>();
        }

        if (IsExpired(state))
        {
            RemoveRequest(state);
            throw new EntityNotFoundException<AppCodeDetailsDto>();
        }

        return state;
    }

    private static void EnsurePending(AppCodeRequestState state)
    {
        if (IsExpired(state))
        {
            RemoveRequest(state);
            throw new BadRequestException<AppCodeDetailsDto>("Application sign-in request has expired.");
        }

        if (state.Status != AppCodeRequestStatus.Pending)
        {
            throw new BadRequestException<AppCodeDetailsDto>("Application sign-in request is no longer pending.");
        }
    }

    private static void RemoveRequest(AppCodeRequestState state)
    {
        state.Tokens = null;
        state.Completion.TrySetResult();
        Requests.Remove(GetCacheKey(state.ApprovalId));
    }

    private static MemoryCacheEntryOptions CreateCacheEntryOptions(DateTime expiresAt)
    {
        return new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(expiresAt)
            .SetSize(1)
            .RegisterPostEvictionCallback(static (_, value, _, _) =>
            {
                if (value is not AppCodeRequestState state)
                {
                    return;
                }

                state.Tokens = null;
                state.Completion.TrySetResult();
            });
    }

    private static string GetCacheKey(Guid approvalId)
    {
        return $"oauth-app-code:{approvalId:D}";
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

    private static (Guid ApprovalId, string PollSecret) ParsePollToken(string? value)
    {
        string? normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            throw new BadRequestException<AppCodePollRequestDto>("PollToken is required.");
        }

        string[] parts = normalized.Split('.', 2);
        if (parts.Length != 2
            || !Guid.TryParse(parts[0], out Guid approvalId)
            || parts[1].Length != PollSecretLength)
        {
            throw new BadRequestException<AppCodePollRequestDto>("PollToken is invalid.");
        }

        return (approvalId, parts[1]);
    }

    private static bool IsPollSecretValid(AppCodeRequestState state, string pollSecret)
    {
        byte[] candidateHash = HashPollSecret(pollSecret);
        return CryptographicOperations.FixedTimeEquals(state.PollSecretHash, candidateHash);
    }

    private static byte[] HashPollSecret(string pollSecret)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(pollSecret));
    }

    private static string CreatePollSecret()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(PollSecretByteLength)).ToLowerInvariant();
    }

    private static string CreatePollToken(Guid approvalId, string pollSecret)
    {
        return $"{approvalId:D}.{pollSecret}";
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

    private class AppCodeRequestState
    {
        public AppCodeRequestState(
            Guid approvalId,
            byte[] pollSecretHash,
            string applicationName,
            string applicationVersion,
            string? deviceName,
            string origin,
            string userAgent,
            DateTime requestedAt,
            DateTime expiresAt)
        {
            ApprovalId = approvalId;
            PollSecretHash = pollSecretHash;
            ApplicationName = applicationName;
            ApplicationVersion = applicationVersion;
            DeviceName = deviceName;
            Origin = origin;
            UserAgent = userAgent;
            RequestedAt = requestedAt;
            ExpiresAt = expiresAt;
        }

        public Guid ApprovalId { get; }

        public byte[] PollSecretHash { get; }

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
}
