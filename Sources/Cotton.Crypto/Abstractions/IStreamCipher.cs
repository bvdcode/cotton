namespace Cotton.Crypto.Abstractions
{
    public interface IStreamCipher
    {
        Task EncryptAsync(Stream input, Stream output, int chunkSize = 1_048_576, CancellationToken ct = default);

        Task DecryptAsync(Stream input, Stream output, CancellationToken ct = default);
    }
}
