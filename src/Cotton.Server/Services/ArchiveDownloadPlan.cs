// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Cotton.Server.Services
{
    public class ArchiveDownloadPlan : IAsyncDisposable
    {
        private readonly FileStream _entries = ArchiveTemporaryFile.Create();
        private readonly StoredZipLengthCalculator _length = new();
        private static readonly byte[] NewLine = [(byte)'\n'];

        public long SizeBytes => _length.TotalLength;
        public int EntryCount => _length.EntryCount;

        public async Task AppendAsync(ArchiveDownloadEntry entry, CancellationToken cancellationToken)
        {
            _length.Add(entry);
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

        public ValueTask DisposeAsync() => _entries.DisposeAsync();
    }
}
