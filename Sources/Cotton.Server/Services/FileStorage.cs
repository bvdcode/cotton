using Cotton.Server.Settings;
using Cotton.Crypto.Abstractions;
using Cotton.Server.Abstractions;
using System.Text.RegularExpressions;
using System.IO.Pipelines;

namespace Cotton.Server.Services
{
    public partial class FileStorage : IStorage
    {
        private readonly string _basePath;
        private readonly IStreamCipher _cipher;
        private readonly CottonSettings _settings;
        private readonly ILogger<FileStorage> _logger;

        [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.Compiled)]
        private static partial Regex CreateHexHashRegex();
        private const string ChunkFileExtension = ".ctn";
        private const string BaseDirectoryName = "chunks";


        public FileStorage(CottonSettings settings, IStreamCipher cipher, ILogger<FileStorage> logger)
        {
            _logger = logger;
            _cipher = cipher;
            _settings = settings;
            _basePath = Path.Combine(AppContext.BaseDirectory, BaseDirectoryName);
            Directory.CreateDirectory(_basePath);
        }

        public async Task<Stream> GetChunkReadStream(string hash, CancellationToken ct = default)
        {
            string dirPath = GetFolderByHash(hash);
            string filePath = Path.Combine(dirPath, hash[4..].ToLowerInvariant() + ChunkFileExtension);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Chunk not found", filePath);
            }
            var fso = new FileStreamOptions
            {
                Mode = FileMode.Open,
                Share = FileShare.Read,
                Access = FileAccess.Read,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan
            };
            var fileStream = new FileStream(filePath, fso);
            var decryptedStream = new MemoryStream(capacity: (int)fileStream.Length);
            await _cipher.DecryptAsync(fileStream, decryptedStream, ct).ConfigureAwait(false);
            decryptedStream.Seek(default, SeekOrigin.Begin);
            await fileStream.DisposeAsync().ConfigureAwait(false);
            return decryptedStream;
        }

        public async Task WriteChunkAsync(string hash, Stream stream, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                throw new ArgumentException("Hash required", nameof(hash));
            }
            ArgumentNullException.ThrowIfNull(stream);

            string dirPath = GetFolderByHash(hash);
            string filePath = Path.Combine(dirPath, hash[4..].ToLowerInvariant() + ChunkFileExtension);
            if (File.Exists(filePath))
            {
                return;
            }

            string tmpFilePath = Path.Combine(dirPath, $"{hash[4..].ToLowerInvariant()}.{Guid.NewGuid():N}.tmp");

            var fso = new FileStreamOptions
            {
                Share = FileShare.None,
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Options = FileOptions.Asynchronous | FileOptions.WriteThrough
            };

            try
            {
                await using var tmp = new FileStream(tmpFilePath, fso);
                if (stream.CanSeek)
                {
                    stream.Seek(default, SeekOrigin.Begin);
                }

                await _cipher.EncryptAsync(stream, tmp, _settings.CipherChunkSizeBytes, ct).ConfigureAwait(false);
                await tmp.FlushAsync(ct).ConfigureAwait(false);
                tmp.Flush(true);
            }
            catch
            {
                TryDelete(tmpFilePath);
                throw;
            }

            try
            {
                if (File.Exists(filePath))
                {
                    TryDelete(tmpFilePath);
                    return;
                }

                File.Move(tmpFilePath, filePath);
                File.SetAttributes(filePath, FileAttributes.ReadOnly | FileAttributes.NotContentIndexed);
            }
            catch (IOException)
            {
                if (File.Exists(filePath))
                {
                    TryDelete(tmpFilePath);
                    return;
                }
                TryDelete(tmpFilePath);
                throw;
            }
            catch
            {
                TryDelete(tmpFilePath);
                throw;
            }
        }

        private string GetFolderByHash(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                throw new ArgumentException("Invalid chunk hash.", nameof(hash));
            }
            string normalized = hash.Trim().ToLowerInvariant();
            if (!CreateHexHashRegex().IsMatch(normalized))
            {
                throw new ArgumentException("Invalid chunk hash.", nameof(hash));
            }
            string p1 = normalized[..2];
            string p2 = normalized[2..4];
            string dirPath = Path.Combine(_basePath, p1, p2);
            Directory.CreateDirectory(dirPath);
            return dirPath;
        }

        private void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete temporary file {Path}", path);
            }
        }

        private Stream CreateDecryptingReadStream(string hash)
        {
            string dirPath = GetFolderByHash(hash);
            string filePath = Path.Combine(dirPath, hash[4..].ToLowerInvariant() + ChunkFileExtension);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Chunk not found", filePath);
            }

            var fso = new FileStreamOptions
            {
                Mode = FileMode.Open,
                Share = FileShare.Read,
                Access = FileAccess.Read,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan
            };

            var pipe = new Pipe();
            var readerStream = pipe.Reader.AsStream();
            var writerStream = pipe.Writer.AsStream();
            var fs = new FileStream(filePath, fso);

            _ = Task.Run(async () =>
            {
                Exception? error = null;
                try
                {
                    await _cipher.DecryptAsync(fs, writerStream).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                finally
                {
                    try { await writerStream.DisposeAsync().ConfigureAwait(false); } catch { }
                    try { await fs.DisposeAsync().ConfigureAwait(false); } catch { }
                    pipe.Writer.Complete(error);
                }
            });

            return readerStream;
        }

        private sealed class ConcatenatedReadStream : Stream
        {
            private readonly IEnumerator<string> _hashes;
            private readonly FileStorage _storage;
            private Stream? _current;

            public ConcatenatedReadStream(FileStorage storage, IEnumerable<string> hashes)
            {
                _storage = storage;
                _hashes = hashes.GetEnumerator();
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            private bool EnsureCurrent()
            {
                while (_current == null)
                {
                    if (!_hashes.MoveNext()) return false;
                    _current = _storage.CreateDecryptingReadStream(_hashes.Current);
                }
                return true;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (!EnsureCurrent()) return 0;
                int read = _current!.Read(buffer, offset, count);
                if (read == 0)
                {
                    _current.Dispose();
                    _current = null;
                    return Read(buffer, offset, count);
                }
                return read;
            }

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (!EnsureCurrent()) return 0;
                int read = await _current!.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    await _current.DisposeAsync().ConfigureAwait(false);
                    _current = null;
                    return await ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                }
                return read;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _current?.Dispose();
                    _hashes.Dispose();
                }
                base.Dispose(disposing);
            }

            public override ValueTask DisposeAsync()
            {
                _current?.Dispose();
                _hashes.Dispose();
                return ValueTask.CompletedTask;
            }

            public override void Flush() => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        public Stream GetBlobStream(string[] hashes)
        {
            ArgumentNullException.ThrowIfNull(hashes);
            return new ConcatenatedReadStream(this, hashes);
        }
    }
}
