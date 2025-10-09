namespace Cotton.Crypto.Abstractions
{
    public interface IStreamCipher
    {
        Task EncryptAsync(Stream input, Stream output, int chunkSize = AesGcmStreamCipher.DefaultChunkSize, CancellationToken ct = default);

        Task DecryptAsync(Stream input, Stream output, CancellationToken ct = default);

        Task<Stream> DecryptToStreamAsync(Stream input, CancellationToken ct = default);
    }
}
