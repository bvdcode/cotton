// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;
using System.Text;

namespace Cotton.Server.Services
{
    public abstract class ProtectedTokenService : IDisposable
    {
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private readonly byte[] _key;
        private readonly byte[] _purpose;
        private readonly string _prefix;
        private readonly int _payloadLength;
        private readonly ILogger _logger;
        private bool _disposed;

        protected ProtectedTokenService(
            CottonEncryptionSettings settings, string purpose, string prefix, int payloadLength, ILogger logger)
        {
            _purpose = Encoding.UTF8.GetBytes(purpose);
            _prefix = prefix;
            _payloadLength = payloadLength;
            _logger = logger;
            byte[] masterKey = Convert.FromBase64String(settings.MasterEncryptionKey);
            try
            {
                _key = KeyDerivation.DeriveSubkey(masterKey, _purpose, 32);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(masterKey);
            }
        }

        protected string Protect(ReadOnlySpan<byte> payload)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentOutOfRangeException.ThrowIfNotEqual(payload.Length, _payloadLength);
            byte[] token = new byte[NonceSize + TagSize + payload.Length];
            Span<byte> nonce = token.AsSpan(0, NonceSize);
            RandomNumberGenerator.Fill(nonce);
            using AesGcm cipher = new(_key, TagSize);
            cipher.Encrypt(nonce, payload, token.AsSpan(NonceSize + TagSize), token.AsSpan(NonceSize, TagSize), _purpose);
            return _prefix + WebEncoders.Base64UrlEncode(token);
        }

        protected bool TryUnprotect(string token, Span<byte> payload)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentOutOfRangeException.ThrowIfNotEqual(payload.Length, _payloadLength);
            int byteLength = NonceSize + TagSize + _payloadLength;
            int encodedLength = ((byteLength * 8) + 5) / 6;
            if (token.Length != _prefix.Length + encodedLength || !token.StartsWith(_prefix, StringComparison.Ordinal))
            {
                return false;
            }
            try
            {
                byte[] bytes = WebEncoders.Base64UrlDecode(token[_prefix.Length..]);
                if (bytes.Length != byteLength)
                {
                    return false;
                }
                using AesGcm cipher = new(_key, TagSize);
                cipher.Decrypt(bytes.AsSpan(0, NonceSize), bytes.AsSpan(NonceSize + TagSize),
                    bytes.AsSpan(NonceSize, TagSize), payload, _purpose);
                return true;
            }
            catch (Exception exception) when (exception is FormatException or CryptographicException)
            {
                _logger.LogDebug("Rejected an invalid token for {Purpose}.", _prefix);
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            CryptographicOperations.ZeroMemory(_key);
            _disposed = true;
        }
    }
}
