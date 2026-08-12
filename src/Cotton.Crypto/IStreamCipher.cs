// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Crypto
{
    public interface IStreamCipher
    {
        Task EncryptAsync(
            Stream input,
            Stream output,
            int chunkSize = AesGcmStreamCipher.DefaultChunkSize,
            bool leaveInputOpen = true,
            bool leaveOutputOpen = true,
            CancellationToken ct = default);

        Task DecryptAsync(
            Stream input,
            Stream output,
            bool leaveInputOpen = true,
            bool leaveOutputOpen = true,
            CancellationToken ct = default);

        Task<Stream> EncryptAsync(
            Stream input,
            int chunkSize = AesGcmStreamCipher.DefaultChunkSize,
            bool leaveOpen = false,
            CancellationToken ct = default);

        Task<Stream> DecryptAsync(
            Stream input,
            bool leaveOpen = false,
            CancellationToken ct = default);
    }
}
