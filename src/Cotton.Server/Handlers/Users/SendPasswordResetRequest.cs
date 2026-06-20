// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Providers;
using EasyExtensions.Helpers;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Users
{
    /// <summary>
    /// Represents the send password reset request payload accepted by the API.
    /// </summary>
    public class SendPasswordResetRequest(string usernameOrEmail, HttpRequest httpRequest) : IRequest
    {
        /// <summary>
        /// Gets the username or email.
        /// </summary>
        public string UsernameOrEmail { get; } = usernameOrEmail;
        /// <summary>
        /// Gets the http request.
        /// </summary>
        public HttpRequest HttpRequest { get; } = httpRequest;
    }

    /// <summary>
    /// Handles send password reset requests in the mediator pipeline.
    /// </summary>
    public class SendPasswordResetRequestHandler(
        CottonDbContext _dbContext,
        INotificationsProvider _notifications,
        SettingsProvider _settingsProvider) : IRequestHandler<SendPasswordResetRequest>
    {
        private const int TokenLength = 32;
        private static readonly TimeSpan CooldownPeriod = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Handles the request through the mediator pipeline.
        /// </summary>
        public async Task Handle(SendPasswordResetRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.UsernameOrEmail))
            {
                return;
            }

            string input = request.UsernameOrEmail.Trim();
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(
                    x => x.Username == input || x.Email == input,
                    cancellationToken);

            if (user == null || string.IsNullOrWhiteSpace(user.Email))
            {
                return;
            }

            if (user.PasswordResetTokenSentAt != null &&
                DateTime.UtcNow - user.PasswordResetTokenSentAt.Value < CooldownPeriod)
            {
                return;
            }

            user.PasswordResetToken = StringHelpers.CreateRandomString(TokenLength);
            user.PasswordResetTokenSentAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            string baseUrl = _settingsProvider.GetServerSettings().PublicBaseUrl;
            var parameters = new Dictionary<string, string>
            {
                ["token"] = user.PasswordResetToken,
            };

            await _notifications.SendEmailAsync(
                user.Id,
                EmailTemplate.PasswordReset,
                parameters,
                baseUrl);

            // Intentionally silent: do not reveal whether user exists or email was sent.
        }
    }
}
