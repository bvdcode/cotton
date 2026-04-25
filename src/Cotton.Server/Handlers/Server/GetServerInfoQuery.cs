using Cotton.Models;
using Cotton.Server.Abstractions;
using Cotton.Server.Providers;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Server
{
    public class GetServerInfoQuery : IRequest<PublicServerInfo>
    {
    }

    public class GetServerInfoQueryHandler(SettingsProvider _settings) : IRequestHandler<GetServerInfoQuery, PublicServerInfo>
    {
        public async Task<PublicServerInfo> Handle(GetServerInfoQuery request, CancellationToken cancellationToken)
        {
            string instanceIdHash = _settings.GetServerSettings().GetInstanceIdHash();
            bool serverHasUsers = await _settings.ServerHasUsersAsync();
            return new PublicServerInfo()
            {
                InstanceIdHash = instanceIdHash,
                CanCreateInitialAdmin = !serverHasUsers,
                Product = Constants.ProductName,
            };
        }
    }
}
