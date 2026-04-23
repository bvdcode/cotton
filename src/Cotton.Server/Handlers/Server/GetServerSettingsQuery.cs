using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Server
{
    public class GetServerSettingsQuery(bool isAdmin) : IRequest<ServerSettingsEnvelopeDto>
    {
        public bool IsAdmin { get; } = isAdmin;
    }

    public class GetServerSettingsQueryHandler(SettingsProvider _settings) : IRequestHandler<GetServerSettingsQuery, ServerSettingsEnvelopeDto>
    {
        public Task<ServerSettingsEnvelopeDto> Handle(GetServerSettingsQuery request, CancellationToken cancellationToken)
        {
            var serverSettings = _settings.GetServerSettings();
            return Task.FromResult(new ServerSettingsEnvelopeDto
            {
                MaxChunkSizeBytes = serverSettings.MaxChunkSizeBytes,
                SupportedHashAlgorithm = Hasher.SupportedHashAlgorithm,
                Settings = request.IsAdmin ? serverSettings : null,
            });
        }
    }
}
