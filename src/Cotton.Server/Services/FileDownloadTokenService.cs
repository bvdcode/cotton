// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace Cotton.Server.Services
{
    public class FileDownloadTokenService(
        CottonEncryptionSettings settings, TimeProvider clock, ILogger<FileDownloadTokenService> logger)
        : ProtectedTokenService(settings, "Cotton.FileDownload.Token.v1", "ctd1.", PayloadLength, logger)
    {
        private const int GuidSize = 16;
        private const int PayloadLength = GuidSize * 2 + sizeof(long);

        public string Create(Guid userId, Guid nodeFileId, TimeSpan lifetime)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);
            ArgumentOutOfRangeException.ThrowIfEqual(userId, Guid.Empty);
            ArgumentOutOfRangeException.ThrowIfEqual(nodeFileId, Guid.Empty);
            Span<byte> payload = stackalloc byte[PayloadLength];
            userId.TryWriteBytes(payload[..GuidSize]);
            nodeFileId.TryWriteBytes(payload.Slice(GuidSize, GuidSize));
            BinaryPrimitives.WriteInt64BigEndian(payload[(GuidSize * 2)..], clock.GetUtcNow().Add(lifetime).ToUnixTimeSeconds());
            return Protect(payload);
        }

        public bool TryRead(string token, [NotNullWhen(true)] out FileDownloadToken? download)
        {
            download = null;
            Span<byte> payload = stackalloc byte[PayloadLength];
            if (!TryUnprotect(token, payload))
            {
                return false;
            }
            long expiresAt = BinaryPrimitives.ReadInt64BigEndian(payload[(GuidSize * 2)..]);
            if (expiresAt <= clock.GetUtcNow().ToUnixTimeSeconds())
            {
                return false;
            }
            download = new FileDownloadToken(new Guid(payload[..GuidSize]),
                new Guid(payload.Slice(GuidSize, GuidSize)), DateTimeOffset.FromUnixTimeSeconds(expiresAt));
            return true;
        }
    }
}
