using Cotton.Crypto.Abstractions;

namespace Cotton.Crypto
{
    public class AesGcmStreamCipher(ReadOnlyMemory<byte> _masterKey) : IStreamCipher
    {
        public Task DecryptAsync(Stream input, Stream output, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task EncryptAsync(Stream input, Stream output, int chunkSize = 1_048_576, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }
}
