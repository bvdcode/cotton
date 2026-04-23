using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Server
{
    public class CreateServerSettingsRequest(ServerSettingsRequestDto settings, string fallbackPublicBaseUrl) : IRequest
    {
        public ServerSettingsRequestDto Settings { get; } = settings;
        public string FallbackPublicBaseUrl { get; } = fallbackPublicBaseUrl;
    }

    public class CreateServerSettingsRequestHandler(SettingsProvider _settings) : IRequestHandler<CreateServerSettingsRequest>
    {
        public async Task Handle(CreateServerSettingsRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Settings.PublicBaseUrl))
            {
                request.Settings.PublicBaseUrl = request.FallbackPublicBaseUrl;
            }

            string? error = await _settings.ValidateServerSettingsAsync(request.Settings);
            if (error is not null)
            {
                throw new BadRequestException(error);
            }

            await _settings.SaveServerSettingsAsync(request.Settings);
        }
    }
}
