namespace Cotton.Crypto.Internals
{
    // Work queue and results for encryption pipeline
    internal readonly struct EncryptionJob(long index, byte[] dataBuffer, int dataLength)
    {
        public long Index { get; } = index;
        public byte[] DataBuffer { get; } = dataBuffer;
        public int DataLength { get; } = dataLength;
    }

    internal readonly struct EncryptionResult(long index, Tag128 tag, byte[] data, int dataLength)
    {
        public long Index { get; } = index;
        public Tag128 Tag { get; } = tag;
        public byte[] Data { get; } = data;
        public int DataLength { get; } = dataLength;
    }

    // Work queue and results for decryption pipeline
    internal readonly struct DecryptionJob(long index, byte[] nonce, byte[] tag, byte[] cipherBuffer, int dataLength)
    {
        public long Index { get; } = index;
        public byte[] Nonce { get; } = nonce;
        public byte[] Tag { get; } = tag;
        public byte[] Cipher { get; } = cipherBuffer;
        public int DataLength { get; } = dataLength;
    }

    internal readonly struct DecryptionResult(long index, byte[] data, int dataLength)
    {
        public long Index { get; } = index;
        public byte[] Data { get; } = data;
        public int DataLength { get; } = dataLength;
    }
}
