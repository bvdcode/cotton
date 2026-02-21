using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Helpers;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Users
{
    public class SendEmailVerificationRequest(Guid userId, HttpRequest httpRequest) : IRequest
    {
        public Guid UserId { get; } = userId;
        public HttpRequest HttpRequest { get; } = httpRequest;
    }

    public class SendEmailVerificationRequestHandler(
        CottonDbContext _dbContext,
        INotificationsProvider _notifications)
        : IRequestHandler<SendEmailVerificationRequest>
    {
        private const int TokenLength = 32;
        private static readonly TimeSpan CooldownPeriod = TimeSpan.FromMinutes(2);

        public async Task Handle(SendEmailVerificationRequest request, CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken)
                ?? throw new EntityNotFoundException<User>();

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                return;
            }

            if (user.IsEmailVerified)
            {
                return;
            }

            if (user.EmailVerificationTokenSentAt != null &&
                DateTime.UtcNow - user.EmailVerificationTokenSentAt.Value < CooldownPeriod)
            {
                return;
            }

            user.EmailVerificationToken = StringHelpers.CreateRandomString(TokenLength);
            user.EmailVerificationTokenSentAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            string baseUrl = $"{request.HttpRequest.Scheme}://{request.HttpRequest.Host}";
            var parameters = new Dictionary<string, string>
            {
                ["token"] = user.EmailVerificationToken,
            };

            await _notifications.SendEmailAsync(
                user.Id,
                EmailTemplate.EmailConfirmation,
                parameters,
                baseUrl);
        }
    }
}
