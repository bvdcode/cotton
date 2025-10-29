using System.Collections.Concurrent;
using Cotton.Server.Abstractions;

namespace Cotton.Server.IntegrationTests.Helpers;

/// <summary>
/// In-memory implementation of IStorage for integration tests.
/// Avoids filesystem side-effects and speeds up IO.
/// </summary>
public sealed class InMemoryStorage : IStorage
{
 private readonly ConcurrentDictionary<string, byte[]> _blobs = new(StringComparer.OrdinalIgnoreCase);

 public Stream GetBlobStream(string[] uids)
 {
 ArgumentNullException.ThrowIfNull(uids);
 MemoryStream ms = new();
 foreach (var uid in uids)
 {
 if (!_blobs.TryGetValue(uid, out var data))
 {
 throw new FileNotFoundException($"Blob {uid} not found in memory storage");
 }
 ms.Write(data,0, data.Length);
 }
 ms.Seek(0, SeekOrigin.Begin);
 return ms;
 }

 public async Task WriteFileAsync(string uid, Stream stream, CancellationToken ct = default)
 {
 ArgumentException.ThrowIfNullOrWhiteSpace(uid);
 ArgumentNullException.ThrowIfNull(stream);
 using var ms = new MemoryStream();
 if (stream.CanSeek)
 {
 stream.Seek(0, SeekOrigin.Begin);
 }
 await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
 _blobs[uid] = ms.ToArray();
 }
}
