using Cotton.Crypto.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cotton.Crypto.Hashers
{
    public class Sha256Hasher : IHasher
    {
        public int HashSize => 32;

        public void ComputeHash(ReadOnlySpan<byte> data, Span<byte> destination)
        {
            throw new NotImplementedException();
        }

        public byte[] ComputeHash(ReadOnlySpan<byte> data)
        {
            throw new NotImplementedException();
        }

        public void ComputeHash(Stream data, Span<byte> destination)
        {
            throw new NotImplementedException();
        }

        public byte[] ComputeHash(Stream data)
        {
            throw new NotImplementedException();
        }
    }
}
