// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Exceptions;

namespace Cotton.Server.Handlers.Auth.AppCode
{
    internal static class AppCodeRequestGuard
    {
        public static AppCodeRequestState Get(AppCodeRequestStore store, Guid id)
        {
            if (!store.TryGet(id, out AppCodeRequestState? state) || state is null)
            {
                throw new EntityNotFoundException<AppCodeDetailsDto>("App sign-in request not found.");
            }

            if (IsExpired(state))
            {
                store.Remove(state);
                throw new EntityNotFoundException<AppCodeDetailsDto>("App sign-in request has expired.");
            }

            return state;
        }

        public static void EnsurePending(AppCodeRequestStore store, AppCodeRequestState state)
        {
            if (IsExpired(state))
            {
                store.Remove(state);
                throw new BadRequestException<AppCodeDetailsDto>("Application sign-in request has expired.");
            }

            if (state.Status != AppCodeRequestStatus.Pending)
            {
                throw new BadRequestException<AppCodeDetailsDto>("Application sign-in request is no longer pending.");
            }
        }

        public static bool IsExpired(AppCodeRequestState state)
        {
            return state.ExpiresAt <= DateTime.UtcNow;
        }
    }
}
