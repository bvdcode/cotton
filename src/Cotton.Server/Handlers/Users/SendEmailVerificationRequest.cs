using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Users
{
    public class SendEmailVerificationRequest(Guid userId) : IRequest
    {
        public Guid UserId { get; } = userId;
    }

    public class SendEmailVerificationRequestHandler : IRequestHandler<SendEmailVerificationRequest>
    {
        public Task Handle(SendEmailVerificationRequest request, CancellationToken cancellationToken)
        {

        }
    }
}
