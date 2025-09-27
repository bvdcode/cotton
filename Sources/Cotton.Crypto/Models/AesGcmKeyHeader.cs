namespace Cotton.Crypto.Models
{
    public readonly record struct AesGcmKeyHeader(int KeyId, byte[] Nonce, byte[] Tag, byte[] EncryptedKey, long DataLength = 0)
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
            writer.Write(DataLength);
            writer.Write(KeyId);
            writer.Write(Nonce);
            writer.Write(Tag);
            writer.Write(EncryptedKey);
            writer.Flush();
            return ms.ToArray();
        }

        public static AesGcmKeyHeader FromStream(Stream stream, int nonceSize, int tagSize)
        {
            using BinaryReader reader = new(stream, System.Text.Encoding.ASCII, leaveOpen: true);
            byte[] magicBytes = reader.ReadBytes(Magic.Length);
            string magic = System.Text.Encoding.ASCII.GetString(magicBytes);
            if (magic != Magic)
            {
                throw new InvalidDataException("Invalid magic number in header.");
            }
            int headerLength = reader.ReadInt32();
            long dataLength = reader.ReadInt64();
            int keyId = reader.ReadInt32();
            byte[] nonce = reader.ReadBytes(nonceSize);
            byte[] tag = reader.ReadBytes(tagSize);
            int encryptedKeyLength = headerLength - (sizeof(int) + sizeof(long) + sizeof(int) + nonceSize + tagSize + Magic.Length);
            byte[] encryptedKey = reader.ReadBytes(encryptedKeyLength);
            if (nonce.Length != nonceSize || tag.Length != tagSize || encryptedKey.Length != encryptedKeyLength)
            {
                throw new InvalidDataException("Invalid header format.");
            }
            return new AesGcmKeyHeader(keyId, nonce, tag, encryptedKey, dataLength);
        }
    }
}
