using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
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
        INotificationsProvider _notifications) : IRequestHandler<SendPasswordResetRequest>
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

            string baseUrl = $"{request.HttpRequest.Scheme}://{request.HttpRequest.Host}";
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
