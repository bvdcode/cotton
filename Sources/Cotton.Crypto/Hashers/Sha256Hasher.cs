using Cotton.Crypto.Abstractions;
using System;
using System.Security.Cryptography;

namespace Cotton.Crypto.Hashers
{
    public class Sha256Hasher : IHasher
    {
        public int HashSize => 32; // bytes

        public void ComputeHash(ReadOnlySpan<byte> data, Span<byte> destination)
        {
            if (destination.Length != HashSize)
            {
                throw new ArgumentException($"Destination span length must be {HashSize} bytes", nameof(destination));
            }
            // Use shared instance for throughput; HashData is static thread-safe.
            if (!SHA256.TryHashData(data, destination, out int written) || written != HashSize)
            {
                throw new CryptographicException("SHA256 hashing failed");
            }
        }

        public byte[] ComputeHash(ReadOnlySpan<byte> data)
        {
            return SHA256.HashData(data);
        }

        public void ComputeHash(Stream data, Span<byte> destination)
        {
            ArgumentNullException.ThrowIfNull(data);
            if (destination.Length != HashSize)
            {
                throw new ArgumentException($"Destination span length must be {HashSize} bytes", nameof(destination));
            }
            // Stream overload not available for TryHashData in older versions; read incrementally.
            Span<byte> buffer = stackalloc byte[8192];
            using var sha = SHA256.Create();
            int read;
            while ((read = data.Read(buffer)) > 0)
            {
                sha.TransformBlock(buffer[..read].ToArray(), 0, read, null, 0);
            }
            sha.TransformFinalBlock([], 0, 0);
            if (sha.Hash is null || sha.Hash.Length != HashSize)
            {
                throw new CryptographicException("SHA256 stream hash failed");
            }
            sha.Hash.CopyTo(destination);
        }

        public byte[] ComputeHash(Stream data)
        {
            ArgumentNullException.ThrowIfNull(data);
            using var sha = SHA256.Create();
            Span<byte> buffer = stackalloc byte[8192];
            int read;
            while ((read = data.Read(buffer)) > 0)
            {
                sha.TransformBlock(buffer[..read].ToArray(), 0, read, null, 0);
            }
            sha.TransformFinalBlock([], 0, 0);
            return sha.Hash ?? throw new CryptographicException("SHA256 stream hash failed");
        }
    }
}
