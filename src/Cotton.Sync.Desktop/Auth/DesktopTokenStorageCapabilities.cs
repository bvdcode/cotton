// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Cotton.Sync.Desktop.Auth;

internal static class DesktopTokenStorageCapabilities
{
    private static readonly byte[] ProbePayload = "Cotton.Sync.Desktop.TokenStorageProbe.v1"u8.ToArray();

    public static DesktopTokenStorageCapabilitySnapshot CreateSnapshot()
    {
        return CreateSnapshot(DesktopTokenPayloadProtectorFactory.CreateDefault());
    }

    public static Task<DesktopTokenStorageCapabilitySnapshot> CreateVerifiedSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        return CreateVerifiedSnapshotAsync(DesktopTokenPayloadProtectorFactory.CreateDefault(), cancellationToken);
    }

    internal static DesktopTokenStorageCapabilitySnapshot CreateSnapshot(ITokenPayloadProtector protector)
    {
        ArgumentNullException.ThrowIfNull(protector);
        return protector switch
        {
            WindowsDpapiTokenPayloadProtector => new DesktopTokenStorageCapabilitySnapshot(
                protector.Scheme,
                IsReleaseSecure: true,
                "Windows DPAPI current-user protection"),
            LinuxSecretServiceTokenPayloadProtector => new DesktopTokenStorageCapabilitySnapshot(
                protector.Scheme,
                IsReleaseSecure: true,
                "Linux Secret Service through secret-tool"),
            RestrictedFileTokenPayloadProtector => new DesktopTokenStorageCapabilitySnapshot(
                protector.Scheme,
                IsReleaseSecure: false,
                "Development fallback: restricted local file without cryptographic protection"),
            _ => new DesktopTokenStorageCapabilitySnapshot(
                protector.Scheme,
                IsReleaseSecure: false,
                "Unknown token payload protector"),
        };
    }

    internal static async Task<DesktopTokenStorageCapabilitySnapshot> CreateVerifiedSnapshotAsync(
        ITokenPayloadProtector protector,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(protector);
        DesktopTokenStorageCapabilitySnapshot snapshot = CreateSnapshot(protector);
        if (!snapshot.IsReleaseSecure)
        {
            return snapshot;
        }

        byte[]? protectedPayload = null;
        IDeletableTokenPayloadProtector? deletableProtector = protector as IDeletableTokenPayloadProtector;
        bool cleanupRequired = false;
        try
        {
            protectedPayload = await protector.ProtectAsync(ProbePayload, cancellationToken).ConfigureAwait(false);
            cleanupRequired = deletableProtector is not null;
            byte[] unprotectedPayload = await protector
                .UnprotectAsync(protectedPayload, cancellationToken)
                .ConfigureAwait(false);
            if (!unprotectedPayload.SequenceEqual(ProbePayload))
            {
                return snapshot with
                {
                    IsReleaseSecure = false,
                    Details = snapshot.Details + " failed verification: roundtrip payload mismatch",
                };
            }

            if (deletableProtector is not null)
            {
                await deletableProtector.DeleteAsync(protectedPayload, cancellationToken).ConfigureAwait(false);
                cleanupRequired = false;
            }

            return snapshot with
            {
                Details = snapshot.Details + " (verified)",
            };
        }
        catch (Exception exception) when (IsTokenStorageProbeException(exception))
        {
            Trace.TraceWarning("Token storage verification failed: {0}", exception);
            return snapshot with
            {
                IsReleaseSecure = false,
                Details = snapshot.Details + " is unavailable: " + exception.Message,
            };
        }
        finally
        {
            if (cleanupRequired && protectedPayload is not null && deletableProtector is not null)
            {
                await TryDeleteProbePayloadAsync(deletableProtector, protectedPayload).ConfigureAwait(false);
            }
        }
    }

    private static async Task TryDeleteProbePayloadAsync(
        IDeletableTokenPayloadProtector protector,
        byte[] protectedPayload)
    {
        try
        {
            await protector.DeleteAsync(protectedPayload, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsTokenStorageProbeException(exception))
        {
            Trace.TraceWarning("Token storage verification cleanup failed: {0}", exception);
        }
    }

    private static bool IsTokenStorageProbeException(Exception exception)
    {
        return exception is CryptographicException
            or PlatformNotSupportedException
            or IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or Win32Exception;
    }
}
