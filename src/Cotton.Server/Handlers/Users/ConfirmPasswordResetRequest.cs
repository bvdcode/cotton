using Cotton.Database;
using Cotton.Database.Models;
using EasyExtensions.Abstractions;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Users
{
    public class ConfirmPasswordResetRequest(string token, string newPassword) : IRequest
    {
        public string Token { get; } = token;
        public string NewPassword { get; } = newPassword;
    }

    public class ConfirmPasswordResetRequestHandler(
        CottonDbContext _dbContext,
        IPasswordHashService _hasher) : IRequestHandler<ConfirmPasswordResetRequest>
    {
        private static readonly TimeSpan TokenExpiration = TimeSpan.FromHours(1);

        public async Task Handle(ConfirmPasswordResetRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Token))
            {
                throw new BadRequestException<User>("Token is required");
            }

            if (string.IsNullOrWhiteSpace(request.NewPassword))
            {
                throw new BadRequestException<User>("New password is required");
            }

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.PasswordResetToken == request.Token, cancellationToken)
                ?? throw new BadRequestException<User>("Invalid or expired token");

            if (user.PasswordResetTokenSentAt == null ||
                DateTime.UtcNow - user.PasswordResetTokenSentAt.Value > TokenExpiration)
            {
                user.PasswordResetToken = null;
                user.PasswordResetTokenSentAt = null;
                await _dbContext.SaveChangesAsync(cancellationToken);
                throw new BadRequestException<User>("Token has expired");
            }

            user.PasswordPhc = _hasher.Hash(request.NewPassword);
            user.PasswordResetToken = null;
            user.PasswordResetTokenSentAt = null;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
