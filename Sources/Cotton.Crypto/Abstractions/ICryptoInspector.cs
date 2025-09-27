using Cotton.Crypto.Models;

namespace Cotton.Crypto.Abstractions
{
    public interface ICryptoInspector
    {
        bool TryReadHeader(Stream input, out CryptoHeader header);
    }
}
