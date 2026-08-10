// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.AspNetCore.Authorization.Models.Dto;

namespace Cotton.Server.Services
{
    internal class AppCodeRequestState(
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
        public Guid ApprovalId { get; } = approvalId;

        public byte[] PollSecretHash { get; } = pollSecretHash;

        public string ApplicationName { get; } = applicationName;

        public string ApplicationVersion { get; } = applicationVersion;

        public string? DeviceName { get; } = deviceName;

        public string Origin { get; } = origin;

        public string UserAgent { get; } = userAgent;

        public DateTime RequestedAt { get; } = requestedAt;

        public DateTime ExpiresAt { get; } = expiresAt;

        public SemaphoreSlim Gate { get; } = new(1, 1);

        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public AppCodeRequestStatus Status { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public TokenPairResponseDto? Tokens { get; set; }
    }
}
