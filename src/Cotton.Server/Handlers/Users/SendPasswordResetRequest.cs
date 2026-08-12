// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Email;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using EasyExtensions.Helpers;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Users
{
    public class SendPasswordResetRequest(string usernameOrEmail, HttpRequest httpRequest) : IRequest
    {
        public string UsernameOrEmail { get; } = usernameOrEmail;

        public HttpRequest HttpRequest { get; } = httpRequest;
    }

    public class SendPasswordResetRequestHandler(
        CottonDbContext _dbContext,
        INotificationsProvider _notifications,
        SettingsProvider _settingsProvider) : IRequestHandler<SendPasswordResetRequest>
    {
        private const int TokenLength = 32;
        private static readonly TimeSpan CooldownPeriod = TimeSpan.FromMinutes(2);

        public async Task Handle(SendPasswordResetRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.UsernameOrEmail))
            {
                return;
            }

            string input = request.UsernameOrEmail.Trim();
            User? user = await _dbContext.Users
                .FirstOrDefaultAsync(
                    x => x.Username == input || x.Email == input,
                    cancellationToken);

            if (user is null || string.IsNullOrWhiteSpace(user.Email))
            {
                return;
            }

            if (user.PasswordResetTokenSentAt is not null &&
                DateTime.UtcNow - user.PasswordResetTokenSentAt.Value < CooldownPeriod)
            {
                return;
            }

            string token = StringHelpers.CreateRandomString(TokenLength);
            user.PasswordResetToken = AuthSessionIssuer.HashRefreshToken(token);
            user.PasswordResetTokenSentAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            string baseUrl = _settingsProvider.GetServerSettings().PublicBaseUrl;
            var parameters = new Dictionary<string, string>
            {
                [EmailTemplateParameterNames.Token] = token,
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
