// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;
using System.Runtime.Versioning;
using System.Text;

namespace Cotton.Sync.Desktop.Auth
{
    [SupportedOSPlatform("windows")]
    internal sealed class WindowsDpapiTokenPayloadProtector : ITokenPayloadProtector
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Cotton.Sync.Desktop.TokenStore.v1");

        public string Scheme => "windows-dpapi-current-user-v1";

        public Task<byte[]> ProtectAsync(byte[] plaintext, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(plaintext);
            cancellationToken.ThrowIfCancellationRequested();
            EnsureWindows();
            return Task.FromResult(ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser));
        }

        public Task<byte[]> UnprotectAsync(byte[] protectedPayload, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(protectedPayload);
            cancellationToken.ThrowIfCancellationRequested();
            EnsureWindows();
            return Task.FromResult(ProtectedData.Unprotect(protectedPayload, Entropy, DataProtectionScope.CurrentUser));
        }

        private static void EnsureWindows()
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("DPAPI token protection is only available on Windows.");
            }
        }
    }
}
