using Cotton.Storage.Abstractions;
using EasyExtensions.Abstractions;

namespace Cotton.Storage.Processors
{
    public class CryptoProcessor(IStreamCipher cipher) : IStorageProcessor
    {
        public int Priority => 10;

        public Task<Stream> ReadAsync(string uid, Stream stream)
        {
            return cipher.DecryptAsync(stream);
        }

        public Task<Stream> WriteAsync(string uid, Stream stream)
        {
            return cipher.EncryptAsync(stream);
        }
    }
}
