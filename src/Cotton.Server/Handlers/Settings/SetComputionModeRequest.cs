// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Settings
{
    public class SetComputionModeRequest(
        ComputionMode mode,
        string fallbackPublicBaseUrl) : IRequest
    {
        public ComputionMode Mode { get; } = mode;
        public string FallbackPublicBaseUrl { get; } = fallbackPublicBaseUrl;
    }

    public class SetComputionModeRequestHandler(
        SettingsProvider _settings,
        ServerSettingsValidator _validator,
        IMediator _mediator) : IRequestHandler<SetComputionModeRequest>
    {
        public async Task Handle(SetComputionModeRequest request, CancellationToken cancellationToken)
        {
            string? error = _validator.ValidateComputionMode(request.Mode);
            if (error is not null)
            {
                throw new BadRequestException<CottonServerSettings>(error);
            }

            ServerSettingsSnapshot settings = _settings.GetServerSettings();
            if (request.Mode == ComputionMode.Remote && string.IsNullOrWhiteSpace(settings.RemoteComputationRunnerUrl))
            {
                throw new BadRequestException<CottonServerSettings>(
                    "Remote computation runner URL must be configured before enabling Remote mode.");
            }

            if (request.Mode == ComputionMode.Remote)
            {
                await _mediator.Send(
                    new SetRemoteComputationRunnerUrlRequest(
                        settings.RemoteComputationRunnerUrl, request.FallbackPublicBaseUrl),
                    cancellationToken);
                return;
            }

            await _settings.SetPropertyAsync(
                x => x.ComputionMode,
                request.Mode,
                request.FallbackPublicBaseUrl,
                cancellationToken);
        }
    }
}
