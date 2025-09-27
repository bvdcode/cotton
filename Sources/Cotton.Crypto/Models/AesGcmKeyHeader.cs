namespace Cotton.Crypto.Models
{
    public readonly record struct AesGcmKeyHeader(int KeyId, byte[] Nonce, byte[] Tag, byte[] EncryptedKey, long DataLength = 0)
    {
        public const string Magic = "CTN1";
        private static readonly byte[] MagicBytes = System.Text.Encoding.ASCII.GetBytes(Magic);

        public ReadOnlyMemory<byte> ToBytes()
        {
            // Magic (4 bytes) + Header Length (4 bytes) + Data Length (8 bytes) + Key ID (4 bytes) + Nonce + Tag + EncryptedKey
            int headerLength = MagicBytes.Length + sizeof(int) + sizeof(long) + sizeof(int) + Nonce.Length + Tag.Length + EncryptedKey.Length;
            byte[] buffer = new byte[headerLength];
            
            int offset = 0;
            
            // Write Magic
            MagicBytes.CopyTo(buffer, offset);
            offset += MagicBytes.Length;
            
            // Write Header Length
            BitConverter.TryWriteBytes(buffer.AsSpan(offset), headerLength);
            offset += sizeof(int);
            
            // Write Data Length
            BitConverter.TryWriteBytes(buffer.AsSpan(offset), DataLength);
            offset += sizeof(long);
            
            // Write Key ID
            BitConverter.TryWriteBytes(buffer.AsSpan(offset), KeyId);
            offset += sizeof(int);
            
            // Write Nonce
            Nonce.CopyTo(buffer, offset);
            offset += Nonce.Length;
            
            // Write Tag
            Tag.CopyTo(buffer, offset);
            offset += Tag.Length;
            
            // Write Encrypted Key
            EncryptedKey.CopyTo(buffer, offset);
            
            return buffer;
        }

        public static AesGcmKeyHeader FromStream(Stream stream, int nonceSize, int tagSize)
        {
            Span<byte> intBuffer = stackalloc byte[sizeof(int)];
            Span<byte> longBuffer = stackalloc byte[sizeof(long)];
            
            // Read and verify magic
            byte[] magicBytes = new byte[Magic.Length];
            int bytesRead = stream.Read(magicBytes);
            if (bytesRead != Magic.Length || !magicBytes.AsSpan().SequenceEqual(MagicBytes))
            {
                throw new InvalidDataException("Invalid magic number in header.");
            }
            
            // Read header length
            stream.ReadExactly(intBuffer);
            int headerLength = BitConverter.ToInt32(intBuffer);
            
            // Read data length
            stream.ReadExactly(longBuffer);
            long dataLength = BitConverter.ToInt64(longBuffer);
            
            // Read key ID
            stream.ReadExactly(intBuffer);
            int keyId = BitConverter.ToInt32(intBuffer);
            
            // Read nonce
            byte[] nonce = new byte[nonceSize];
            stream.ReadExactly(nonce);
            
            // Read tag
            byte[] tag = new byte[tagSize];
            stream.ReadExactly(tag);
            
            // Read encrypted key
            int encryptedKeyLength = headerLength - (sizeof(int) + sizeof(long) + sizeof(int) + nonceSize + tagSize + Magic.Length);
            byte[] encryptedKey = new byte[encryptedKeyLength];
            stream.ReadExactly(encryptedKey);
            
            return new AesGcmKeyHeader(keyId, nonce, tag, encryptedKey, dataLength);
        }
    }
}
