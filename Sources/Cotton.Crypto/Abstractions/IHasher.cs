namespace Cotton.Crypto.Abstractions
{
    public interface IHasher
    {
        public int HashSize { get; }
        public void ComputeHash(ReadOnlySpan<byte> data, Span<byte> destination);
        public byte[] ComputeHash(ReadOnlySpan<byte> data);
        public void ComputeHash(Stream data, Span<byte> destination);
        public byte[] ComputeHash(Stream data);
    }
}