// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Auth.AppCode
{
    public record GetAppCodeRequest(Guid Id) : IRequest<AppCodeDetailsDto>;

    public class GetAppCodeRequestHandler(AppCodeRequestStore store)
        : IRequestHandler<GetAppCodeRequest, AppCodeDetailsDto>
    {
        public Task<AppCodeDetailsDto> Handle(GetAppCodeRequest request, CancellationToken cancellationToken)
        {
            AppCodeRequestState state = AppCodeRequestGuard.Get(store, request.Id);
            AppCodeDetailsDto details = new()
            {
                Id = state.ApprovalId,
                ApplicationName = state.ApplicationName,
                ApplicationVersion = state.ApplicationVersion,
                DeviceName = state.DeviceName,
                Origin = state.Origin,
                RequestedAt = state.RequestedAt,
                ExpiresAt = state.ExpiresAt,
                Status = state.Status.ToString().ToLowerInvariant(),
            };
            return Task.FromResult(details);
        }
    }
}
