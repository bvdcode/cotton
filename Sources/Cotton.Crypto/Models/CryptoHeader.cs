using Cotton.Crypto.Flags;

namespace Cotton.Crypto.Models
{
    public readonly record struct CryptoHeader(int KeyId, byte[] Nonce, byte[] EncryptedFileKey);
}
