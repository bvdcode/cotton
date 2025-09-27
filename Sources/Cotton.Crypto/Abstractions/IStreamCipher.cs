namespace Cotton.Crypto.Abstractions
{
    public interface IStreamCipher
    {
        Task EncryptAsync(Stream input, Stream output, ReadOnlyMemory<byte> masterKey, int chunkSize, CancellationToken ct = default);

        Task DecryptAsync(Stream input, Stream output, ReadOnlySpan<byte> masterKey, CancellationToken ct = default);
    }
}
