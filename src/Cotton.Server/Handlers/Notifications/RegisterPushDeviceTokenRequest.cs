// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Cotton.Server.Handlers.Notifications
{
    /// <summary>
    /// Represents a push device token registration request sent through the mediator pipeline.
    /// </summary>
    public class RegisterPushDeviceTokenRequest(
        Guid userId,
        string sessionId,
        PushDeviceTokenRegistrationRequestDto payload) : IRequest<PushDeviceTokenDto>
    {
        /// <summary>
        /// Gets the owning user identifier.
        /// </summary>
        public Guid UserId { get; } = userId;
        /// <summary>
        /// Gets the auth session identifier.
        /// </summary>
        public string SessionId { get; } = sessionId;
        /// <summary>
        /// Gets the request payload.
        /// </summary>
        public PushDeviceTokenRegistrationRequestDto Payload { get; } = payload;
    }

    /// <summary>
    /// Handles push device token registration requests in the mediator pipeline.
    /// </summary>
    public class RegisterPushDeviceTokenRequestHandler(
        CottonDbContext _dbContext) : IRequestHandler<RegisterPushDeviceTokenRequest, PushDeviceTokenDto>
    {
        /// <summary>
        /// Handles the request through the mediator pipeline.
        /// </summary>
        public async Task<PushDeviceTokenDto> Handle(
            RegisterPushDeviceTokenRequest request,
            CancellationToken cancellationToken)
        {
            string token = NormalizeRequired(
                request.Payload.Token,
                nameof(request.Payload.Token),
                PushDeviceToken.TokenMaxLength);
            string sessionId = NormalizeRequired(
                request.SessionId,
                nameof(request.SessionId),
                PushDeviceToken.SessionIdMaxLength);
            string tokenHash = ComputeTokenHash(token);
            DateTime now = DateTime.UtcNow;

            ValidateEnum(request.Payload.Provider, nameof(request.Payload.Provider));
            ValidateEnum(request.Payload.Platform, nameof(request.Payload.Platform));

            PushDeviceToken? deviceToken = await _dbContext.PushDeviceTokens
                .FirstOrDefaultAsync(
                    x => x.Provider == request.Payload.Provider
                        && x.TokenHash == tokenHash,
                    cancellationToken);

            if (deviceToken is null)
            {
                deviceToken = new PushDeviceToken
                {
                    UserId = request.UserId,
                    Provider = request.Payload.Provider,
                    TokenHash = tokenHash,
                };
                await _dbContext.PushDeviceTokens.AddAsync(deviceToken, cancellationToken);
            }
            else
            {
                deviceToken.UserId = request.UserId;
            }

            await RevokeSupersededSessionTokensAsync(request, sessionId, tokenHash, now, cancellationToken);

            deviceToken.Platform = request.Payload.Platform;
            deviceToken.Token = token;
            deviceToken.SessionId = sessionId;
            deviceToken.DeviceName = NormalizeOptional(
                request.Payload.DeviceName,
                PushDeviceToken.DeviceNameMaxLength);
            deviceToken.AppVersion = NormalizeOptional(
                request.Payload.AppVersion,
                PushDeviceToken.AppVersionMaxLength);
            deviceToken.LastRegisteredAt = now;
            deviceToken.RevokedAt = null;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return CreateDto(deviceToken);
        }

        private static void ValidateEnum<TEnum>(TEnum value, string name)
            where TEnum : struct, Enum
        {
            if (!Enum.IsDefined(value))
            {
                throw new BadRequestException<PushDeviceToken>($"{name} is invalid");
            }
        }

        private async Task RevokeSupersededSessionTokensAsync(
            RegisterPushDeviceTokenRequest request,
            string sessionId,
            string tokenHash,
            DateTime revokedAt,
            CancellationToken cancellationToken)
        {
            List<PushDeviceToken> supersededTokens = await _dbContext.PushDeviceTokens
                .Where(x => x.UserId == request.UserId
                    && x.SessionId == sessionId
                    && x.Provider == request.Payload.Provider
                    && x.Platform == request.Payload.Platform
                    && x.TokenHash != tokenHash
                    && x.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (PushDeviceToken supersededToken in supersededTokens)
            {
                supersededToken.RevokedAt = revokedAt;
            }
        }

        private static string NormalizeRequired(string? value, string name, int maxLength)
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new BadRequestException<PushDeviceToken>($"{name} is required");
            }
            if (normalized.Length > maxLength)
            {
                throw new BadRequestException<PushDeviceToken>($"{name} is too long");
            }
            return normalized;
        }

        private static string? NormalizeOptional(string? value, int maxLength)
        {
            string? normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (normalized is null)
            {
                return null;
            }
            return normalized.Length <= maxLength
                ? normalized
                : normalized[..maxLength];
        }

        private static string ComputeTokenHash(string token)
        {
            byte[] hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
            return Convert.ToHexStringLower(hash);
        }

        private static PushDeviceTokenDto CreateDto(PushDeviceToken token)
        {
            return new PushDeviceTokenDto
            {
                Id = token.Id,
                Provider = token.Provider,
                Platform = token.Platform,
                SessionId = token.SessionId,
                DeviceName = token.DeviceName,
                AppVersion = token.AppVersion,
                LastRegisteredAt = token.LastRegisteredAt,
                RevokedAt = token.RevokedAt
            };
        }
    }
}
