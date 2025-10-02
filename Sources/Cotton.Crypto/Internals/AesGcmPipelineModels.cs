namespace Cotton.Crypto.Internals
{
    // Work queue and results for encryption pipeline
    internal readonly struct EncryptionJob(long index, byte[] dataBuffer, int dataLength)
    {
        public long Index { get; } = index;
        public byte[] DataBuffer { get; } = dataBuffer;
        public int DataLength { get; } = dataLength;
    }

    internal readonly struct EncryptionResult(long index, byte[] tag, byte[] data, int dataLength)
    {
        public long Index { get; } = index;
        public byte[] Tag { get; } = tag; // raw 16 bytes, rented from pool
        public byte[] Data { get; } = data; // ciphertext buffer, rented from pool
        public int DataLength { get; } = dataLength;
    }

    // Work queue and results for decryption pipeline
    internal readonly struct DecryptionJob(long index, byte[] tag, byte[] cipherBuffer, int dataLength)
    {
        public long Index { get; } = index;
        public byte[] Tag { get; } = tag; // raw 16 bytes
        public byte[] Cipher { get; } = cipherBuffer; // rented from pool
        public int DataLength { get; } = dataLength;
    }

    internal readonly struct DecryptionResult(long index, byte[] data, int dataLength)
    {
        public long Index { get; } = index;
        public byte[] Data { get; } = data; // rented from pool
        public int DataLength { get; } = dataLength;
    }
}
