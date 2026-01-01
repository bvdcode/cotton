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
            var decryptedStream = await cipher.DecryptAsync(stream);
            var buffer = new MemoryStream();
            await decryptedStream.CopyToAsync(buffer);
            buffer.Position = 0;
            return buffer;
        }

        public async Task<Stream> WriteAsync(string uid, Stream stream)
        {
            var encryptedStream = await cipher.EncryptAsync(stream);
            var buffer = new MemoryStream();
            await encryptedStream.CopyToAsync(buffer);
            buffer.Position = 0;
            return buffer;
        }
    }
}
