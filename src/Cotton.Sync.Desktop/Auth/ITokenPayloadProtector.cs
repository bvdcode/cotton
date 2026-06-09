// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Auth
{
    internal interface ITokenPayloadProtector
    {
        string Scheme { get; }

        Task<byte[]> ProtectAsync(byte[] plaintext, CancellationToken cancellationToken = default);

        Task<byte[]> UnprotectAsync(byte[] protectedPayload, CancellationToken cancellationToken = default);
    }
}
