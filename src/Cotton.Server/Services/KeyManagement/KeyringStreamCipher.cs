// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using EasyExtensions.Abstractions;
using System.Buffers.Binary;

namespace Cotton.Server.Services.KeyManagement;

/// <summary>
/// IStreamCipher implementation backed by a keyring resolver.
/// </summary>
internal sealed class KeyringStreamCipher(
    IKeyringKeyResolver _resolver,
    KeyringKeyPurpose _purpose = KeyringKeyPurpose.ChunkAead,
    int? _threads = null) : IStreamCipher
{
    private const int MinimumHeaderBytesForKeyId = 20;
    private static ReadOnlySpan<byte> CurrentMagic => "CTN2"u8;
    private static ReadOnlySpan<byte> LegacyMagic => "CTN1"u8;

    public async Task EncryptAsync(
        Stream input,
        Stream output,
        int chunkSize = AesGcmStreamCipher.DefaultChunkSize,
        bool leaveInputOpen = true,
        bool leaveOutputOpen = true,
        CancellationToken ct = default)
    {
        KeyringResolvedKey key = _resolver.GetPrimary(_purpose);
        using AesGcmStreamCipher cipher = CreateCipher(key);
        await cipher.EncryptAsync(input, output, chunkSize, leaveInputOpen, leaveOutputOpen, ct);
    }

    public async Task DecryptAsync(
        Stream input,
        Stream output,
        bool leaveInputOpen = true,
        bool leaveOutputOpen = true,
        CancellationToken ct = default)
    {
        PreparedDecryptStream prepared = await PrepareDecryptStreamAsync(input, leaveInputOpen, ct);
        try
        {
            KeyringResolvedKey key = ResolveDecryptKey(prepared.KeyId);
            using AesGcmStreamCipher cipher = CreateCipher(key);
            await cipher.DecryptAsync(
                prepared.Stream,
                output,
                leaveInputOpen: prepared.PassLeaveInputOpenToCipher,
                leaveOutputOpen,
                ct);
        }
        catch
        {
            prepared.DisposeIfCipherWillNotOwnIt();
            throw;
        }
    }

    public async Task<Stream> EncryptAsync(
        Stream input,
        int chunkSize = AesGcmStreamCipher.DefaultChunkSize,
        bool leaveOpen = false,
        CancellationToken ct = default)
    {
        KeyringResolvedKey key = _resolver.GetPrimary(_purpose);
        AesGcmStreamCipher cipher = CreateCipher(key);
        try
        {
            Stream stream = await cipher.EncryptAsync(input, chunkSize, leaveOpen, ct);
            return new CipherOwnedReadStream(stream, cipher);
        }
        catch
        {
            cipher.Dispose();
            throw;
        }
    }

    public async Task<Stream> DecryptAsync(
        Stream input,
        bool leaveOpen = false,
        CancellationToken ct = default)
    {
        PreparedDecryptStream prepared = await PrepareDecryptStreamAsync(input, leaveOpen, ct);
        AesGcmStreamCipher? cipher = null;
        try
        {
            KeyringResolvedKey key = ResolveDecryptKey(prepared.KeyId);
            cipher = CreateCipher(key);
            Stream stream = await cipher.DecryptAsync(prepared.Stream, prepared.PassLeaveInputOpenToCipher, ct);
            return new CipherOwnedReadStream(stream, cipher);
        }
        catch
        {
            prepared.DisposeIfCipherWillNotOwnIt();
            cipher?.Dispose();
            throw;
        }
    }

    private KeyringResolvedKey ResolveDecryptKey(int keyId)
    {
        KeyringResolvedKey key = _resolver.GetById(_purpose, keyId);
        if (key.Status is not (KeyringKeyStatus.EncryptDecrypt or KeyringKeyStatus.DecryptOnly))
        {
            throw new InvalidOperationException($"Keyring key {_purpose}/{keyId} is not enabled for decryption.");
        }

        return key;
    }

    private AesGcmStreamCipher CreateCipher(KeyringResolvedKey key)
    {
        if (key.Algorithm != KeyringFormat.Aes256Gcm)
        {
            throw new InvalidOperationException($"Keyring key {key.Purpose}/{key.Id} uses unsupported algorithm {key.Algorithm}.");
        }

        try
        {
            return new AesGcmStreamCipher(key.Material, key.Id, _threads);
        }
        finally
        {
            Array.Clear(key.Material);
        }
    }

    private static async Task<PreparedDecryptStream> PrepareDecryptStreamAsync(
        Stream input,
        bool leaveInputOpen,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.CanRead)
        {
            throw new ArgumentException("Input stream must be readable.", nameof(input));
        }

        byte[] prefix = new byte[MinimumHeaderBytesForKeyId];
        long? originalPosition = input.CanSeek ? input.Position : null;
        int read = await ReadPrefixAsync(input, prefix, ct);
        if (read < MinimumHeaderBytesForKeyId)
        {
            throw new InvalidDataException("Encrypted Cotton stream is too short to contain a key id.");
        }

        int keyId = ReadKeyId(prefix);
        if (originalPosition.HasValue)
        {
            input.Position = originalPosition.Value;
            return new PreparedDecryptStream(keyId, input, leaveInputOpen);
        }

        return new PreparedDecryptStream(
            keyId,
            new PrefixReadStream(prefix, input, leaveInputOpen),
            PassLeaveInputOpenToCipher: false);
    }

    private static int ReadKeyId(ReadOnlySpan<byte> prefix)
    {
        ReadOnlySpan<byte> magic = prefix[..4];
        if (!magic.SequenceEqual(CurrentMagic) && !magic.SequenceEqual(LegacyMagic))
        {
            throw new InvalidDataException("Invalid Cotton encrypted stream magic.");
        }

        return BinaryPrimitives.ReadInt32LittleEndian(prefix.Slice(16, 4));
    }

    private static async Task<int> ReadPrefixAsync(Stream input, byte[] prefix, CancellationToken ct)
    {
        int offset = 0;
        while (offset < prefix.Length)
        {
            int read = await input.ReadAsync(prefix.AsMemory(offset, prefix.Length - offset), ct);
            if (read == 0)
            {
                break;
            }

            offset += read;
        }

        return offset;
    }

    private sealed record PreparedDecryptStream(
        int KeyId,
        Stream Stream,
        bool PassLeaveInputOpenToCipher)
    {
        public void DisposeIfCipherWillNotOwnIt()
        {
            if (!PassLeaveInputOpenToCipher)
            {
                Stream.Dispose();
            }
        }
    }

    private sealed class CipherOwnedReadStream(Stream _inner, IDisposable _owner) : Stream
    {
        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override int Read(Span<byte> buffer) => _inner.Read(buffer);

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                _owner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class PrefixReadStream(byte[] _prefix, Stream _inner, bool _leaveInnerOpen) : Stream
    {
        private int _prefixOffset;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            int copied = CopyPrefix(buffer);
            if (copied == buffer.Length)
            {
                return copied;
            }

            return copied + _inner.Read(buffer[copied..]);
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            int copied = CopyPrefix(buffer.Span);
            if (copied == buffer.Length)
            {
                return copied;
            }

            return copied + await _inner.ReadAsync(buffer[copied..], cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_leaveInnerOpen)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }

        private int CopyPrefix(Span<byte> destination)
        {
            int remaining = _prefix.Length - _prefixOffset;
            if (remaining <= 0 || destination.Length == 0)
            {
                return 0;
            }

            int count = Math.Min(destination.Length, remaining);
            _prefix.AsSpan(_prefixOffset, count).CopyTo(destination);
            _prefixOffset += count;
            return count;
        }
    }
}
