// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;
using EasyExtensions.Abstractions;

namespace Cotton.Storage.Processors
{
    /// <summary>
    /// Storage processor that encrypts outgoing blob streams and decrypts incoming blob streams.
    /// </summary>
    public class CryptoProcessor(IStreamCipher cipher) : IStorageProcessor
    {
        /// <inheritdoc />
        public int Priority => 1000;

        /// <inheritdoc />
        public Task<Stream> ReadAsync(string uid, Stream stream, PipelineContext? context = null)
        {
            return cipher.DecryptAsync(stream);
        }

        /// <inheritdoc />
        public Task<Stream> WriteAsync(string uid, Stream stream, PipelineContext? context = null)
        {
            return cipher.EncryptAsync(stream);
        }
    }
}
