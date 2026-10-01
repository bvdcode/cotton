// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Auth.AppCode
{
    public record DenyAppCodeRequest(Guid Id) : IRequest;

    public class DenyAppCodeRequestHandler(AppCodeRequestStore store) : IRequestHandler<DenyAppCodeRequest>
    {
        public async Task Handle(DenyAppCodeRequest request, CancellationToken cancellationToken)
        {
            AppCodeRequestState state = AppCodeRequestGuard.Get(store, request.Id);
            await state.Gate.WaitAsync(cancellationToken);
            try
            {
                AppCodeRequestGuard.EnsurePending(store, state);
                state.Status = AppCodeRequestStatus.Denied;
                state.Completion.TrySetResult();
            }
            finally
            {
                state.Gate.Release();
            }
        }
    }
}
