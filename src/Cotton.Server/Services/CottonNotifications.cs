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
using Microsoft.AspNetCore.SignalR;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;

namespace Cotton.Server.Services
{
    public class CottonNotifications(
        CottonDbContext _dbContext,
        SettingsProvider _settingsProvider,
        CottonPublicEmailProvider _publicEmailProvider,
        ILogger<CottonNotifications> _logger,
        IHubContext<EventHub> _hubContext) : INotificationsProvider
    {
        public async Task SendEmailAsync(
            Guid userId,
            EmailTemplate template,
            Dictionary<string, string> parameters,
            string serverBaseUrl)
        {
            var settings = _settingsProvider.GetServerSettings();
            switch (settings.EmailMode)
            {
                case EmailMode.None:
                    return;

                case EmailMode.Cloud:
                    await SendViaCloudAsync(userId, template, parameters, serverBaseUrl, settings);
                    return;

                case EmailMode.Custom:
                    await SendViaSmtpAsync(userId, template, parameters, serverBaseUrl, settings);
                    return;

                default:
                    _logger.LogError("Invalid email mode configured: {EmailMode}.", settings.EmailMode);
                    return;
            }
        }

        private async Task SendViaCloudAsync(
            Guid userId,
            EmailTemplate template,
            Dictionary<string, string> parameters,
            string serverBaseUrl,
            CottonServerSettings settings)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null || string.IsNullOrWhiteSpace(user.Email))
            {
                return;
            }

            bool sent = await _publicEmailProvider.SendEmailAsync(
                template,
                serverBaseUrl,
                user.Email,
                user.Username,
                "en",
                parameters);

            if (!sent)
            {
                _logger.LogWarning(
                    "Failed to send {Template} email via cloud provider for user {UserId}.",
                    template,
                    userId);
            }
        }

        private async Task SendViaSmtpAsync(
            Guid userId,
            EmailTemplate template,
            Dictionary<string, string> parameters,
            string serverBaseUrl,
            CottonServerSettings settings)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null || string.IsNullOrWhiteSpace(user.Email))
            {
                return;
            }

            string token = parameters.GetValueOrDefault("token") ?? string.Empty;
            var variables = EmailTemplateRenderer.BuildVariables(
                user.Username,
                user.Email,
                token,
                serverBaseUrl);

            foreach (var kvp in parameters)
            {
                if (!variables.ContainsKey(kvp.Key))
                {
                    variables[kvp.Key] = kvp.Value;
                }
            }

            string languageCode = "en";
            string subject = EmailTemplateRenderer.GetSubject(template, languageCode);
            string body = EmailTemplateRenderer.Render(template, languageCode, variables);

            SendSmtpEmail(user.Email, user.Username, subject, body, settings);
        }

        private void SendSmtpEmail(
            string recipientEmail,
            string recipientName,
            string subject,
            string body,
            CottonServerSettings settings)
        {
            string host = settings.SmtpServerAddress
                ?? throw new InvalidOperationException("SMTP server address is not configured.");
            int port = settings.SmtpServerPort
                ?? throw new InvalidOperationException("SMTP server port is not configured.");
            string username = settings.SmtpUsername
                ?? throw new InvalidOperationException("SMTP username is not configured.");
            string senderEmail = settings.SmtpSenderEmail
                ?? throw new InvalidOperationException("SMTP sender email is not configured.");
            string? password = _settingsProvider.DecryptValue(settings.SmtpPasswordEncrypted);

            using SmtpClient client = new()
            {
                Host = host,
                Port = port,
                Timeout = 15000,
                EnableSsl = settings.SmtpUseSsl,
                Credentials = new NetworkCredential(username, password)
            };

            using MailMessage mailMessage = new()
            {
                From = new MailAddress(senderEmail, Constants.ProductName),
                Subject = subject,
            };

            var recipient = new MailAddress(recipientEmail, recipientName);
            mailMessage.To.Add(recipient);

            bool isHtml = body.Contains("<html", StringComparison.OrdinalIgnoreCase);
            if (!isHtml)
            {
                mailMessage.Body = body;
                mailMessage.IsBodyHtml = false;
            }
            else
            {
                var htmlView = AlternateView.CreateAlternateViewFromString(
                    body, Encoding.UTF8, MediaTypeNames.Text.Html);
                mailMessage.AlternateViews.Add(htmlView);
                mailMessage.IsBodyHtml = true;
            }

            client.Send(mailMessage);
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
