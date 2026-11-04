// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Cotton.Server.Services
{
    public class ArchiveDownloadPlan : IAsyncDisposable
    {
        private readonly FileStream _entries = ArchiveTemporaryFile.Create();
        private readonly StoredZipLengthCalculator _length = new();
        private readonly IncrementalHash _identity = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private static readonly byte[] NewLine = [(byte)'\n'];

        public long SizeBytes => _length.TotalLength;
        public int EntryCount => _length.EntryCount;
        public long CentralDirectoryOffset => _length.CentralDirectoryOffset;
        public string EntityTag => $"\"zip-v1-{Convert.ToHexStringLower(_identity.GetCurrentHash())}\"";

        public async Task AppendAsync(ArchiveDownloadEntry entry, CancellationToken cancellationToken)
        {
            _length.Add(entry);
            AppendIdentity(entry);
            await JsonSerializer.SerializeAsync(_entries, entry, cancellationToken: cancellationToken);
            await _entries.WriteAsync(NewLine, cancellationToken);
        }

        public async IAsyncEnumerable<ArchiveDownloadEntry> ReadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await _entries.FlushAsync(cancellationToken);
            _entries.Position = 0;
            using StreamReader reader = new(_entries, Encoding.UTF8, leaveOpen: true);
            while (await reader.ReadLineAsync(cancellationToken) is string line)
            {
                yield return JsonSerializer.Deserialize<ArchiveDownloadEntry>(line)
                    ?? throw new InvalidDataException("Archive plan entry is missing.");
            }
        }

        private void AppendIdentity(ArchiveDownloadEntry entry)
        {
            byte[] path = Encoding.UTF8.GetBytes(entry.Path);
            Span<byte> fields = stackalloc byte[13];
            BinaryPrimitives.WriteInt32LittleEndian(fields, path.Length);
            BinaryPrimitives.WriteInt64LittleEndian(fields[4..], entry.SizeBytes);
            fields[12] = entry.IsDirectory ? (byte)1 : (byte)0;
            _identity.AppendData(fields);
            _identity.AppendData(path);
            if (entry is ArchiveDownloadFileEntry file)
            {
                BinaryPrimitives.WriteInt32LittleEndian(fields, file.ChunkHashes.Count);
                _identity.AppendData(fields[..4]);
                foreach (string hash in file.ChunkHashes)
                {
                    _identity.AppendData(Convert.FromHexString(hash));
                    BinaryPrimitives.WriteInt64LittleEndian(fields, file.ChunkLengths[hash]);
                    _identity.AppendData(fields[..8]);
                }
            }
        }

        public ValueTask DisposeAsync()
        {
            _identity.Dispose();
            return _entries.DisposeAsync();
        }
    }
}
