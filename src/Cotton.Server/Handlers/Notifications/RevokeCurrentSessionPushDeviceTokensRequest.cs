// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Notifications
{
    /// <summary>
    /// Represents a current-session push device token revocation request.
    /// </summary>
    public class RevokeCurrentSessionPushDeviceTokensRequest(
        Guid userId,
        string sessionId) : IRequest<PushDeviceTokenRevocationResultDto>
    {
        /// <summary>
        /// Gets the owning user identifier.
        /// </summary>
        public Guid UserId { get; } = userId;
        /// <summary>
        /// Gets the auth session identifier.
        /// </summary>
        public string SessionId { get; } = sessionId;
    }

    /// <summary>
    /// Handles current-session push device token revocation requests in the mediator pipeline.
    /// </summary>
    public class RevokeCurrentSessionPushDeviceTokensRequestHandler(
        PushDeviceTokenRevocationService _pushDeviceTokenRevocations)
        : IRequestHandler<RevokeCurrentSessionPushDeviceTokensRequest, PushDeviceTokenRevocationResultDto>
    {
        /// <summary>
        /// Handles the request through the mediator pipeline.
        /// </summary>
        public async Task<PushDeviceTokenRevocationResultDto> Handle(
            RevokeCurrentSessionPushDeviceTokensRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.SessionId))
            {
                throw new BadRequestException<PushDeviceToken>("Session id is required");
            }

            DateTime revokedAt = DateTime.UtcNow;
            int revokedTokens = await _pushDeviceTokenRevocations.RevokeSessionTokensAsync(
                request.UserId,
                request.SessionId,
                revokedAt,
                cancellationToken);
            return new PushDeviceTokenRevocationResultDto
            {
                RevokedTokens = revokedTokens
            };
        }
    }
}
