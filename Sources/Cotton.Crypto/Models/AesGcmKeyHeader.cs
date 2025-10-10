namespace Cotton.Crypto.Models
{
    public readonly record struct AesGcmKeyHeader(int KeyId, byte[] Nonce, byte[] Tag, byte[] EncryptedKey, long DataLength = 0)
    {
        public const string Magic = "CTN1"; // stay at version 1
        private static readonly byte[] MagicBytes = System.Text.Encoding.ASCII.GetBytes(Magic);

        public ReadOnlyMemory<byte> ToBytes()
        {
            // Magic (4) + Header Length (4) + Data Length (8) + Key ID (4) + NoncePrefix (4) + Nonce + Tag + EncryptedKey
            int headerLength = MagicBytes.Length + sizeof(int) + sizeof(long) + sizeof(int) + sizeof(uint) + Nonce.Length + Tag.Length + EncryptedKey.Length;
            byte[] buffer = new byte[headerLength];
            int offset = 0;
            MagicBytes.CopyTo(buffer, offset);
            offset += MagicBytes.Length;
            BitConverter.TryWriteBytes(buffer.AsSpan(offset), headerLength);
            offset += sizeof(int);
            BitConverter.TryWriteBytes(buffer.AsSpan(offset), DataLength);
            offset += sizeof(long);
            BitConverter.TryWriteBytes(buffer.AsSpan(offset), KeyId);
            offset += sizeof(int);
            // Nonce prefix placeholder (0)
            BitConverter.TryWriteBytes(buffer.AsSpan(offset), (uint)0);
            offset += sizeof(uint);
            Nonce.CopyTo(buffer, offset);
            offset += Nonce.Length;
            Tag.CopyTo(buffer, offset);
            offset += Tag.Length;
            EncryptedKey.CopyTo(buffer, offset);
            return buffer;
        }

        public static AesGcmKeyHeader FromStream(Stream stream, int nonceSize, int tagSize)
        {
            Span<byte> intBuffer = stackalloc byte[sizeof(int)];
            Span<byte> longBuffer = stackalloc byte[sizeof(long)];

            byte[] magicBytes = new byte[MagicBytes.Length];
            int bytesRead = stream.Read(magicBytes);
            if (bytesRead != MagicBytes.Length || !magicBytes.AsSpan().SequenceEqual(MagicBytes))
            {
                throw new InvalidDataException("Invalid magic number in header.");
            }

            stream.ReadExactly(intBuffer);
            int headerLength = BitConverter.ToInt32(intBuffer);

            stream.ReadExactly(longBuffer);
            long dataLength = BitConverter.ToInt64(longBuffer);

            stream.ReadExactly(intBuffer);
            int keyId = BitConverter.ToInt32(intBuffer);

            int fixedPrefix = MagicBytes.Length + sizeof(int) + sizeof(long) + sizeof(int);
            int remaining = headerLength - fixedPrefix;

            if (remaining == tagSize)
            {
                byte[] tag = new byte[tagSize];
                stream.ReadExactly(tag);
                return new AesGcmKeyHeader(keyId, [], tag, [], dataLength);
            }
            else if (remaining > (sizeof(uint) + nonceSize + tagSize))
            {
                // Read file header with dynamic encrypted key length
                stream.ReadExactly(intBuffer); // noncePrefix (uint)
                byte[] nonce = new byte[nonceSize];
                stream.ReadExactly(nonce);
                byte[] tag = new byte[tagSize];
                stream.ReadExactly(tag);
                int encKeyLen = remaining - (sizeof(uint) + nonceSize + tagSize);
                if (encKeyLen <= 0) throw new InvalidDataException("Invalid encrypted key length in header.");
                byte[] encryptedKey = new byte[encKeyLen];
                stream.ReadExactly(encryptedKey);
                return new AesGcmKeyHeader(keyId, nonce, tag, encryptedKey, dataLength);
            }
            else
            {
                throw new InvalidDataException("Unsupported header layout or length.");
            }
        }
    }
}
