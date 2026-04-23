using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Server
{
    public class EmergencyShutdownRequest : IRequest
    {
    }

    public class EmergencyShutdownRequestHandler : IRequestHandler<EmergencyShutdownRequest>
    {
        public Task Handle(EmergencyShutdownRequest request, CancellationToken cancellationToken)
        {
            Environment.Exit(1);
            return Task.CompletedTask;
        }
    }
}
