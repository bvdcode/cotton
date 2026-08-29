// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;

namespace Cotton.Storage.Processors
{
    public class CryptoProcessor(
        IStreamCipher _cipher,
        IEncryptionChunkSizeProvider _chunkSizeProvider) : IStorageProcessor
    {
        public CryptoProcessor(IStreamCipher cipher)
            : this(cipher, new StaticEncryptionChunkSizeProvider(AesGcmStreamCipher.DefaultChunkSize))
        {
        }

        public int Priority => 1000;

        public Task<Stream> ReadAsync(string uid, Stream stream, PipelineContext? context = null)
        {
            return _cipher.DecryptAsync(stream);
        }

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
