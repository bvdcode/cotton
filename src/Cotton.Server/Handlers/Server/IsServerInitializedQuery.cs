using Cotton.Server.Providers;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Server
{
    public class IsServerInitializedQuery : IRequest<bool>
    {
    }

    public class IsServerInitializedQueryHandler(SettingsProvider _settings) : IRequestHandler<IsServerInitializedQuery, bool>
    {
        public async Task<bool> Handle(IsServerInitializedQuery request, CancellationToken cancellationToken)
        {
            return await _settings.IsServerInitializedAsync();
        }
    }
}
