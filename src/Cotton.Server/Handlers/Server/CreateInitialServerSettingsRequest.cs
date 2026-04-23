using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Server
{
    public class CreateInitialServerSettingsRequest(InitialServerSettingsRequestDto settings, string fallbackPublicBaseUrl) : IRequest
    {
        public InitialServerSettingsRequestDto Settings { get; } = settings;
        public string FallbackPublicBaseUrl { get; } = fallbackPublicBaseUrl;
    }

    public class CreateInitialServerSettingsRequestHandler(SettingsProvider _settings)
        : IRequestHandler<CreateInitialServerSettingsRequest>
    {
        public async Task Handle(CreateInitialServerSettingsRequest request, CancellationToken cancellationToken)
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
