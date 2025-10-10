namespace Cotton.Crypto.Abstractions
{
    // Zero-copy byte chunk for channel-based pipelines. The receiver owns returning the buffer to the shared pool.
    public readonly struct ByteChunk
    {
        public byte[] Buffer { get; }
        public int Length { get; }

        public ByteChunk(byte[] buffer, int length)
        {
            Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            Length = length;
        }
    }
}
