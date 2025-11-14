using Cotton.Storage.Abstractions;
using System.Collections.Concurrent;

namespace Cotton.Server.IntegrationTests.Helpers;

/// <summary>
/// In-memory implementation of IStorage for integration tests.
/// Avoids filesystem side-effects and speeds up IO.
/// </summary>
public sealed class InMemoryStorage : IStoragePipeline
{
    private readonly ConcurrentDictionary<string, byte[]> _blobs = new(StringComparer.OrdinalIgnoreCase);

    public Task<Stream> ReadAsync(string uid)
    {
        ArgumentNullException.ThrowIfNull(uid);
        MemoryStream ms = new();
        if (_blobs.TryGetValue(uid, out var data))
        {
            ms.Write(data, 0, data.Length);
            ms.Seek(0, SeekOrigin.Begin);
        }
        else
        {
            throw new FileNotFoundException("Blob not found in in-memory storage", uid);
        }
        return Task.FromResult(result: (Stream)ms);
    }

    public async Task WriteAsync(string uid, Stream stream)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uid);
        ArgumentNullException.ThrowIfNull(stream);
        using var ms = new MemoryStream();
        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }
        await stream.CopyToAsync(ms).ConfigureAwait(false);
        _blobs[uid] = ms.ToArray();
    }
}
