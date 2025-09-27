namespace Cotton.Crypto.Models
{
    public readonly record struct AesGcmKeyHeader(int KeyId, byte[] Nonce, byte[] Tag, byte[] EncryptedKey, long dataLength = 0)
    {
        public const string Magic = "CTN1";

        public ReadOnlyMemory<byte> ToBytes()
        {
            // Magic (4 bytes)
            // Header Length (4 bytes)
            // Data Length (8 bytes)
            // Key ID (4 bytes)
            // Nonce (12 bytes)
            // Tag (16 bytes)
            // Encrypted Key (32 bytes)

            byte[] magicBytes = System.Text.Encoding.ASCII.GetBytes(Magic);
            int headerLength = magicBytes.Length + sizeof(int) + sizeof(long) + sizeof(int) + Nonce.Length + Tag.Length + EncryptedKey.Length;
            using MemoryStream ms = new(headerLength);
            using BinaryWriter writer = new(ms);
            writer.Write(magicBytes);
            writer.Write(headerLength);
            writer.Write(dataLength);
            writer.Write(KeyId);
            writer.Write(Nonce);
            writer.Write(Tag);
            writer.Write(EncryptedKey);
            writer.Flush();
            return ms.ToArray();
        }
    }
}
