// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.Passkeys.Models;

namespace Cotton.Server.Services.Passkeys
{
    internal record PasskeyAssertionState(Guid? ScopedUserId, AuthenticationOptions Options);
}
