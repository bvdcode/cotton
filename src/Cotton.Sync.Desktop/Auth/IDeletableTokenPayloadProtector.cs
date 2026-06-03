// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Auth;

internal interface IDeletableTokenPayloadProtector
{
    Task DeleteAsync(byte[] protectedPayload, CancellationToken cancellationToken = default);
}
