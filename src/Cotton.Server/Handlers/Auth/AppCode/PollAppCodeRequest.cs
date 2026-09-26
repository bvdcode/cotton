// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Auth.AppCode
{
    public record PollAppCodeRequest(string? PollToken) : IRequest<AppCodePollResult>;

    public class PollAppCodeRequestHandler(AppCodeRequestStore store)
        : IRequestHandler<PollAppCodeRequest, AppCodePollResult>
    {
        private static readonly TimeSpan LongPollTimeout = TimeSpan.FromSeconds(25);

        public async Task<AppCodePollResult> Handle(PollAppCodeRequest request, CancellationToken cancellationToken)
        {
            (Guid approvalId, string pollSecret) = AppCodePollToken.Parse(request.PollToken);
            if (!store.TryGet(approvalId, out AppCodeRequestState? state)
                || state is null || !AppCodePollToken.IsValid(state, pollSecret))
            {
                return new AppCodePollResult(AppCodePollStatus.NotFound);
            }

            if (state.Status == AppCodeRequestStatus.Pending && !AppCodeRequestGuard.IsExpired(state))
            {
                TimeSpan remaining = state.ExpiresAt - DateTime.UtcNow;
                TimeSpan wait = remaining <= LongPollTimeout ? remaining : LongPollTimeout;
                if (wait > TimeSpan.Zero)
                {
                    await Task.WhenAny(state.Completion.Task, Task.Delay(wait, cancellationToken));
                }
            }

            await state.Gate.WaitAsync(cancellationToken);
            try
            {
                if (AppCodeRequestGuard.IsExpired(state))
                {
                    store.Remove(state);
                    return new AppCodePollResult(AppCodePollStatus.Expired);
                }

                if (state.Status == AppCodeRequestStatus.Denied)
                {
                    store.Remove(state);
                    return new AppCodePollResult(AppCodePollStatus.Denied);
                }

                if (state.Status == AppCodeRequestStatus.Approved && state.Tokens is not null)
                {
                    TokenPairResponseDto tokens = state.Tokens;
                    state.Status = AppCodeRequestStatus.Consumed;
                    state.Tokens = null;
                    store.Remove(state);
                    return new AppCodePollResult(AppCodePollStatus.Approved, tokens);
                }

                return new AppCodePollResult(AppCodePollStatus.Pending);
            }
            finally
            {
                state.Gate.Release();
            }
        }
    }
}
