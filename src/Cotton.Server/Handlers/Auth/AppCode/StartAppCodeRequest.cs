// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using System.Net;

namespace Cotton.Server.Handlers.Auth.AppCode
{
    public record StartAppCodeRequest(
        AppCodeStartRequestDto Payload,
        IPAddress OriginAddress,
        string UserAgent) : IRequest<AppCodeStartResponseDto?>;

    public class StartAppCodeRequestHandler(AppCodeRequestStore store)
        : IRequestHandler<StartAppCodeRequest, AppCodeStartResponseDto?>
    {
        private static readonly TimeSpan RequestLifetime = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

        public Task<AppCodeStartResponseDto?> Handle(StartAppCodeRequest request, CancellationToken cancellationToken)
        {
            string applicationName = NormalizeRequired(request.Payload.ApplicationName, "ApplicationName", 120);
            string applicationVersion = NormalizeOptional(request.Payload.ApplicationVersion, 80) ?? "Unknown version";
            string? deviceName = NormalizeOptional(request.Payload.DeviceName, 160);
            Guid approvalId = Guid.NewGuid();
            DateTime now = DateTime.UtcNow;
            DateTime expiresAt = now.Add(RequestLifetime);
            (string pollToken, byte[] pollSecretHash) = AppCodePollToken.Create(approvalId);
            AppCodeRequestState state = new(
                approvalId,
                pollSecretHash,
                applicationName,
                applicationVersion,
                deviceName,
                request.OriginAddress.ToString(),
                request.UserAgent,
                now,
                expiresAt);
            if (!store.TryAdd(state))
            {
                return Task.FromResult<AppCodeStartResponseDto?>(null);
            }

            AppCodeStartResponseDto response = new()
            {
                ApprovalId = approvalId,
                ApprovalUrl = $"/oauth/app-code/{approvalId:D}",
                PollToken = pollToken,
                ExpiresAt = expiresAt,
                PollIntervalSeconds = (int)PollInterval.TotalSeconds,
            };
            return Task.FromResult<AppCodeStartResponseDto?>(response);
        }

        private static string NormalizeRequired(string? value, string fieldName, int maxLength)
        {
            return NormalizeOptional(value, maxLength)
                ?? throw new BadRequestException<AppCodeStartRequestDto>($"{fieldName} is required.");
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
    }
}
