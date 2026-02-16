using Cotton.Database;
using Cotton.Database.Models;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Users
{
    public class ConfirmEmailVerificationRequest(string token) : IRequest
    {
        public string Token { get; } = token;
    }

    public class ConfirmEmailVerificationRequestHandler(
        CottonDbContext _dbContext) : IRequestHandler<ConfirmEmailVerificationRequest>
    {
        private static readonly TimeSpan TokenExpiration = TimeSpan.FromHours(24);

        public async Task Handle(ConfirmEmailVerificationRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Token))
            {
                throw new BadRequestException<User>("Token is required");
            }

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.EmailVerificationToken == request.Token, cancellationToken)
                ?? throw new BadRequestException<User>("Invalid or expired token");

            if (user.EmailVerificationTokenSentAt == null ||
                DateTime.UtcNow - user.EmailVerificationTokenSentAt.Value > TokenExpiration)
            {
                user.EmailVerificationToken = null;
                user.EmailVerificationTokenSentAt = null;
                await _dbContext.SaveChangesAsync(cancellationToken);
                throw new BadRequestException<User>("Token has expired");
            }

            user.IsEmailVerified = true;
            user.EmailVerificationToken = null;
            user.EmailVerificationTokenSentAt = null;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
