using Cotton.Storage.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Cotton.Storage.Processors
{
    public class S3StorageProcessor : IStorageProcessor
    {
        public int Priority => 10;

        public Task<Stream> ReadAsync(string uid, Stream stream)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> WriteAsync(string uid, Stream stream)
        {
            throw new NotImplementedException();
        }
    }
}
