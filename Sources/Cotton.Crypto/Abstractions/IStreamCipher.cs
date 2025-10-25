﻿// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

namespace Cotton.Crypto.Abstractions
{
    public interface IStreamCipher
    {
        Task EncryptAsync(Stream input, Stream output, int chunkSize = AesGcmStreamCipher.DefaultChunkSize, CancellationToken ct = default);

        Task DecryptAsync(Stream input, Stream output, CancellationToken ct = default);
    }
}
