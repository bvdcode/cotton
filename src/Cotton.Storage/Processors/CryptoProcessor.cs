// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using EasyExtensions.Abstractions;

namespace Cotton.Storage.Processors
{
    public class CryptoProcessor(IStreamCipher cipher) : IStorageProcessor
    {
        public int Priority => 1000;

        public async Task<Stream> ReadAsync(string uid, Stream stream)
        {
            return await cipher.DecryptAsync(stream);
        }

        public async Task<Stream> WriteAsync(string uid, Stream stream)
        {
            return await cipher.EncryptAsync(stream);
        }
    }
}
