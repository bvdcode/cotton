using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
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
        IHubContext<EventHub> _hubContext) : INotificationsProvider
    {
        public Task SendEmailAsync(Guid userId, string subject, string body)
        {
            var settings = _settingsProvider.GetServerSettings();
            if (settings.EmailMode == EmailMode.None)
            {
                return Task.CompletedTask;
            }
            if (settings.EmailMode == EmailMode.Cloud)
            {
                throw new NotImplementedException("Cloud email sending is not implemented yet.");
            }
            if (settings.EmailMode == EmailMode.Custom)
            {
                return SendEmailAsync(userId, subject, body, settings);
            }
            throw new InvalidOperationException("Invalid email mode configured.");
        }

        private async Task SendEmailAsync(Guid userId, string subject, string body, CottonServerSettings settings)
        {
            var foundUser = await _dbContext.Users.FindAsync(userId)
                ?? throw new EntityNotFoundException<User>();
            if (string.IsNullOrWhiteSpace(foundUser.Email))
            {
                return;
            }
            string host = settings.SmtpServerAddress ?? throw new InvalidOperationException("SMTP server address is not configured.");
            int port = settings.SmtpServerPort ?? throw new InvalidOperationException("SMTP server port is not configured.");
            string username = settings.SmtpUsername ?? throw new InvalidOperationException("SMTP username is not configured.");
            string senderEmail = settings.SmtpSenderEmail ?? throw new InvalidOperationException("SMTP sender email is not configured.");
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
                From = new MailAddress(senderEmail, "Cotton Cloud"),
                Subject = subject,
            };
            var recipient = new MailAddress(foundUser.Email, foundUser.Username);
            mailMessage.To.Add(recipient);
            bool isHtml = body.Contains("<html", StringComparison.OrdinalIgnoreCase);

            if (!isHtml)
            {
                mailMessage.Body = body;
                mailMessage.IsBodyHtml = false;
            }
            else
            {
                var htmlView = AlternateView.CreateAlternateViewFromString(body, Encoding.UTF8, MediaTypeNames.Text.Html);
                //if (body.Contains($"cid:{EmailLetterBuilder.LogoContentId}", StringComparison.OrdinalIgnoreCase))
                //{
                //    byte[] iconBytes = Constants.GetCompanyIconPngBytes();
                //    var stream = new MemoryStream(iconBytes);
                //    var logoResource = new LinkedResource(stream, MediaTypeNames.Image.Png)
                //    {
                //        ContentId = EmailLetterBuilder.LogoContentId,
                //        TransferEncoding = TransferEncoding.Base64
                //    };

                //    htmlView.LinkedResources.Add(logoResource);
                //}

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
