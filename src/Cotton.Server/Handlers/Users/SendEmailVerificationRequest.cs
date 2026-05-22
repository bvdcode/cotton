// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Providers;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Helpers;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Users
{
    /// <summary>
    /// Represents the send email verification request request payload accepted by the API.
    /// </summary>
    public class SendEmailVerificationRequest(Guid userId) : IRequest
    {
        /// <summary>
        /// Gets the owning user identifier.
        /// </summary>
        public Guid UserId { get; } = userId;
    }

    /// <summary>
    /// Handles send email verification requests in the mediator pipeline.
    /// </summary>
    public class SendEmailVerificationRequestHandler(
        CottonDbContext _dbContext,
        INotificationsProvider _notifications,
        SettingsProvider _settingsProvider)
        : IRequestHandler<SendEmailVerificationRequest>
    {
        private const int TokenLength = 32;
        private static readonly TimeSpan CooldownPeriod = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Handles the request through the mediator pipeline.
        /// </summary>
        public async Task Handle(SendEmailVerificationRequest request, CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken)
                ?? throw new EntityNotFoundException<User>();

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                throw new BadRequestException<User>("No email address is set for this account.");
            }

            if (user.IsEmailVerified)
            {
                throw new BadRequestException<User>("Email is already verified.");
            }

            if (user.EmailVerificationTokenSentAt != null &&
                DateTime.UtcNow - user.EmailVerificationTokenSentAt.Value < CooldownPeriod)
            {
                throw new BadRequestException<User>("Verification email was already sent. Please wait before requesting again.");
            }

            user.EmailVerificationToken = StringHelpers.CreateRandomString(TokenLength);
            user.EmailVerificationTokenSentAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            string baseUrl = _settingsProvider.GetServerSettings().PublicBaseUrl;
            var parameters = new Dictionary<string, string>
            {
                ["token"] = user.EmailVerificationToken,
            };

            bool sent = await _notifications.SendEmailAsync(
                user.Id,
                EmailTemplate.EmailConfirmation,
                parameters,
                baseUrl);

            if (!sent)
            {
                throw new BadRequestException<User>("Failed to send verification email. Please try again later.");
            }
        }
    }
}
