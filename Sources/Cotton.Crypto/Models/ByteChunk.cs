namespace Cotton.Crypto.Models
{
    // Adapter structures and streams used to bridge decryption pipeline to a readable Stream
    internal readonly struct ByteChunk(byte[] buffer, int length)
    {
        public byte[] Buffer { get; } = buffer;
        public int Length { get; } = length;
    }
}
