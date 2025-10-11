namespace Cotton.Crypto.Abstractions
{
    // Zero-copy byte chunk for channel-based pipelines. The receiver owns returning the buffer to the shared pool.
    public readonly struct ByteChunk(byte[] buffer, int length)
    {
        public byte[] Buffer { get; } = buffer ?? throw new ArgumentNullException(nameof(buffer));
        public int Length { get; } = length;
    }
}
