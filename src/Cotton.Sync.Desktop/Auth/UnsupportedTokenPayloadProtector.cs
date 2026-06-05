// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Auth;

internal class UnsupportedTokenPayloadProtector : ITokenPayloadProtector
{
    public UnsupportedTokenPayloadProtector(string scheme, string details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
        ArgumentException.ThrowIfNullOrWhiteSpace(details);

        Scheme = scheme;
        Details = details;
    }

    public string Scheme { get; }

    public string Details { get; }

    public Task<byte[]> ProtectAsync(byte[] plaintext, CancellationToken cancellationToken = default)
    {
        throw new PlatformNotSupportedException(Details);
    }

    public Task<byte[]> UnprotectAsync(byte[] protectedPayload, CancellationToken cancellationToken = default)
    {
        throw new PlatformNotSupportedException(Details);
    }
}
