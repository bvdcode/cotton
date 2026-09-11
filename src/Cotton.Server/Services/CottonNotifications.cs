// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Email;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Hubs;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using EasyExtensions.AspNetCore.Exceptions;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace Cotton.Server.Services
{
    public class CottonNotifications(
        CottonDbContext _dbContext,
        SettingsProvider _settingsProvider,
        ServerSettingsValidator _settingsValidator,
        CottonPublicEmailProvider _publicEmailProvider,
        ILogger<CottonNotifications> _logger,
        IHubContext<EventHub> _hubContext) : INotificationsProvider
    {
        public async Task<bool> SendEmailAsync(
            Guid userId,
            EmailTemplate template,
            Dictionary<string, string> parameters,
            string serverBaseUrl,
            string? recipientEmail = null)
        {
            ServerSettingsSnapshot settings = _settingsProvider.GetServerSettings();
            switch (settings.EmailMode)
            {
                case EmailMode.None:
                    _logger.LogInformation("Email mode is None — skipping {Template} for user {UserId}.", template, userId);
                    return false;

                case EmailMode.Cloud:
                    return await SendViaCottonBridgeAsync(userId, template, parameters, serverBaseUrl, settings, recipientEmail);

                case EmailMode.Custom:
                    return await SendViaSmtpAsync(userId, template, parameters, serverBaseUrl, settings, recipientEmail);

                default:
                    _logger.LogError("Invalid email mode configured: {EmailMode}.", settings.EmailMode);
                    return false;
            }
        }

        public async Task SendSmtpTestEmailAsync(
            Guid userId,
            string serverBaseUrl)
        {
            ServerSettingsSnapshot settings = _settingsProvider.GetServerSettings();
            EmailConfig emailConfig = new EmailConfig
            {
                SmtpServer = settings.SmtpServerAddress ?? string.Empty,
                Port = settings.SmtpServerPort?.ToString() ?? string.Empty,
                Username = settings.SmtpUsername ?? string.Empty,
                Password = settings.SmtpPassword ?? string.Empty,
                FromAddress = settings.SmtpSenderEmail ?? string.Empty,
                UseSSL = settings.SmtpUseSsl,
            };

            string? validationError = _settingsValidator.ValidateEmailConfig(emailConfig);
            if (validationError is not null)
            {
                _logger.LogWarning("SMTP test email validation failed: {ValidationError}", validationError);
                throw new ArgumentException(validationError);
            }

            User? user = await _dbContext.Users.FindAsync(userId);
            if (user is null || string.IsNullOrWhiteSpace(user.Email))
            {
                throw new EntityNotFoundException<User>("User not found or does not have an email address.");
            }

            string recipientName = GetRecipientDisplayName(user);
            string subject = "Cotton SMTP test email";
            string body = BuildSmtpTestBody(recipientName, serverBaseUrl);
            SendSmtpEmail(user.Email, recipientName, subject, body, emailConfig);
        }

        private async Task<bool> SendViaCottonBridgeAsync(
            Guid userId,
            EmailTemplate template,
            Dictionary<string, string> parameters,
            string serverBaseUrl,
            ServerSettingsSnapshot settings,
            string? recipientEmail)
        {
            if (!settings.TelemetryEnabled)
            {
                _logger.LogInformation("Telemetry is disabled — skipping {Template} for user {UserId}.", template, userId);
                return false;
            }
            User? user = await _dbContext.Users.FindAsync(userId);
            if (user is null)
            {
                return false;
            }

            string email = ResolveRecipientEmail(user, recipientEmail);
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            string recipientName = GetRecipientDisplayName(user);

            bool sent = await _publicEmailProvider.SendEmailAsync(
                settings.InstanceId,
                template,
                serverBaseUrl,
                email,
                recipientName,
                "en",
                parameters);

            if (!sent)
            {
                _logger.LogWarning(
                    "Failed to send {Template} email via Cotton Bridge for user {UserId}.",
                    template,
                    userId);
            }

            return sent;
        }

        private async Task<bool> SendViaSmtpAsync(
            Guid userId,
            EmailTemplate template,
            Dictionary<string, string> parameters,
            string serverBaseUrl,
            ServerSettingsSnapshot settings,
            string? recipientEmail)
        {
            User? user = await _dbContext.Users.FindAsync(userId);
            if (user is null)
            {
                return false;
            }

            string email = ResolveRecipientEmail(user, recipientEmail);
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            string token = parameters.GetValueOrDefault(EmailTemplateParameterNames.Token) ?? string.Empty;
            string recipientName = GetRecipientDisplayName(user);
            Dictionary<string, string> variables = EmailTemplateRenderer.BuildVariables(
                recipientName,
                email,
                token,
                serverBaseUrl);

            foreach (KeyValuePair<string, string> kvp in parameters)
            {
                if (!variables.ContainsKey(kvp.Key))
                {
                    variables[kvp.Key] = kvp.Value;
                }
            }

            string languageCode = "en";
            string subject = EmailTemplateRenderer.GetSubject(template, languageCode);
            string body = EmailTemplateRenderer.Render(template, languageCode, variables);

            try
            {
                SendSmtpEmail(email, recipientName, subject, body, CreateEmailConfig(settings));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send {Template} email via SMTP to {Email}.", template, email);
                return false;
            }
        }

        private static string ResolveRecipientEmail(User user, string? recipientEmail)
        {
            return string.IsNullOrWhiteSpace(recipientEmail)
                ? user.Email ?? string.Empty
                : recipientEmail.Trim();
        }

        private static string GetRecipientDisplayName(User user)
        {
            string? firstName = string.IsNullOrWhiteSpace(user.FirstName) ? null : user.FirstName.Trim();
            string? lastName = string.IsNullOrWhiteSpace(user.LastName) ? null : user.LastName.Trim();

            if (firstName is null && lastName is null)
            {
                return user.Username;
            }

            if (firstName is null)
            {
                return lastName!;
            }

            if (lastName is null)
            {
                return firstName;
            }

            return firstName + " " + lastName;
        }

        private static string BuildSmtpTestBody(string recipientName, string serverBaseUrl)
        {
            string displayName = string.IsNullOrWhiteSpace(recipientName)
                ? "there"
                : recipientName;
            string baseUrl = serverBaseUrl.Trim().TrimEnd('/');

            return
                "Hi " + displayName + "," + Environment.NewLine + Environment.NewLine +
                "This is a test email from Cotton to confirm your SMTP configuration works." + Environment.NewLine +
                "Server: " + baseUrl + Environment.NewLine +
                "Sent at (UTC): " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + Environment.NewLine +
                "If you received this message, your SMTP setup is ready.";
        }

        private static void SendSmtpEmail(
            string recipientEmail,
            string recipientName,
            string subject,
            string body,
            EmailConfig settings)
        {
            if (!ServerSettingsValidator.TryParsePort(settings.Port, out int port))
            {
                throw new InvalidOperationException("SMTP port is not configured.");
            }

            using SmtpClient client = new()
            {
                Host = settings.SmtpServer,
                Port = port,
                Timeout = 15000,
                EnableSsl = settings.UseSSL,
                Credentials = new NetworkCredential(settings.Username, settings.Password)
            };

            using MailMessage mailMessage = new()
            {
                From = new MailAddress(settings.FromAddress, Constants.ProductName),
                Subject = subject,
                SubjectEncoding = Encoding.UTF8,
                Body = body,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = body.Contains("<html", StringComparison.OrdinalIgnoreCase),
            };

            MailAddress recipient = new MailAddress(recipientEmail, recipientName);
            mailMessage.To.Add(recipient);

            client.Send(mailMessage);
        }

        private static EmailConfig CreateEmailConfig(ServerSettingsSnapshot settings)
        {
            return new EmailConfig
            {
                SmtpServer = settings.SmtpServerAddress ?? string.Empty,
                Port = settings.SmtpServerPort?.ToString() ?? string.Empty,
                Username = settings.SmtpUsername ?? string.Empty,
                Password = settings.SmtpPassword ?? string.Empty,
                FromAddress = settings.SmtpSenderEmail ?? string.Empty,
                UseSSL = settings.SmtpUseSsl,
            };
        }

        public async Task SendNotificationAsync(
            Guid userId,
            string title,
            string? content = null,
            NotificationPriority priority = NotificationPriority.None,
            Dictionary<string, string>? metadata = null)
        {
            Notification notification = new()
            {
                Title = title,
                UserId = userId,
                Content = content,
                Priority = priority,
                Metadata = metadata ?? []
            };
            await _dbContext.Notifications.AddAsync(notification);
            await _dbContext.SaveChangesAsync();
            await _hubContext.Clients.User(userId.ToString()).SendAsync(
                EventHub.NotificationMethod,
                notification.Adapt<NotificationDto>());
        }
    }
}
