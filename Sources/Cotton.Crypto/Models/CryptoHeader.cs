using Cotton.Crypto.Flags;

namespace Cotton.Crypto.Models
{
    public readonly record struct CryptoHeader(byte Version, byte KeyId, int ChunkSize, CryptoFlags Flags);
}
