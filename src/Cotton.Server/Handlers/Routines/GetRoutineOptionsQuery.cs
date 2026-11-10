// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Routines
{
    public record GetRoutineOptionsQuery : IRequest<RoutineOptionsDto>;

    public class GetRoutineOptionsQueryHandler(SettingsProvider settings) : IRequestHandler<GetRoutineOptionsQuery, RoutineOptionsDto>
    {
        public Task<RoutineOptionsDto> Handle(GetRoutineOptionsQuery request, CancellationToken ct)
            => Task.FromResult(new RoutineOptionsDto(settings.GetServerSettings().GetTimezoneInfo().Id));
    }
}
