// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Auth
{

    internal sealed class RestrictedFileTokenPayloadProtector : ITokenPayloadProtector
    {
        public string Scheme => "restricted-file-v1";

        public Task<byte[]> ProtectAsync(byte[] plaintext, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(plaintext);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(plaintext.ToArray());
        }

        public Task<byte[]> UnprotectAsync(byte[] protectedPayload, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(protectedPayload);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(protectedPayload.ToArray());
        }
    }
}
