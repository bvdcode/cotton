using Cotton.Crypto.Abstractions;
using Cotton.Storage.Abstractions;
using System.IO;
using System.IO.Pipelines;

namespace Cotton.Storage.Processors
{
    public class CryptoProcessor(IStreamCipher cipher) : IStorageProcessor
    {
        public Task<Stream> ReadAsync(string uid, Stream stream)
        {

            var decryptedStream = new MemoryStream(capacity: (int)fileStream.Length);
            await _cipher.DecryptAsync(fileStream, decryptedStream, ct).ConfigureAwait(false);
            decryptedStream.Seek(default, SeekOrigin.Begin);
        }

        public Task<Stream> WriteAsync(string uid, Stream stream)
        {
            throw new NotImplementedException();
        }
        private Stream CreateDecryptingReadStream(string uid)
        {
            uid = NormalizeIdentity(uid);
            string dirPath = GetFolderByUid(uid);
            string filePath = Path.Combine(dirPath, uid[4..] + ChunkFileExtension);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("File not found", filePath);
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
                    try
                    {
                        await writerStream.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to dispose writer stream");
                    }
                    try
                    {
                        await fs.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to dispose file stream");
                    }
                    pipe.Writer.Complete(error);
                }
            });

            return readerStream;
        }
    }
}
