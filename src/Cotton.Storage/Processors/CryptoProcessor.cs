// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;

namespace Cotton.Storage.Processors
{
    /// <summary>
    /// Storage processor that encrypts outgoing blob streams and decrypts incoming blob streams.
    /// </summary>
    public class CryptoProcessor : IStorageProcessor
    {
        private readonly IStreamCipher _cipher;
        private readonly IEncryptionChunkSizeProvider _chunkSizeProvider;

        /// <summary>
        /// Initializes the processor with the default AES-GCM chunk size.
        /// </summary>
        public CryptoProcessor(IStreamCipher cipher)
            : this(cipher, new StaticEncryptionChunkSizeProvider(AesGcmStreamCipher.DefaultChunkSize))
        {
        }

        /// <summary>
        /// Initializes the processor with a runtime AES-GCM chunk-size provider.
        /// </summary>
        public CryptoProcessor(IStreamCipher cipher, IEncryptionChunkSizeProvider chunkSizeProvider)
        {
            _cipher = cipher;
            _chunkSizeProvider = chunkSizeProvider;
        }

        /// <inheritdoc />
        public int Priority => 1000;

        /// <inheritdoc />
        public Task<Stream> ReadAsync(string uid, Stream stream, PipelineContext? context = null)
        {
            return _cipher.DecryptAsync(stream);
        }

        /// <inheritdoc />
        public Task<Stream> WriteAsync(string uid, Stream stream, PipelineContext? context = null)
        {
            return _cipher.EncryptAsync(stream, _chunkSizeProvider.ChunkSizeBytes);
        }

        private class StaticEncryptionChunkSizeProvider(int chunkSizeBytes) : IEncryptionChunkSizeProvider
        {
            public int ChunkSizeBytes { get; } = chunkSizeBytes;
        }
    }
}
