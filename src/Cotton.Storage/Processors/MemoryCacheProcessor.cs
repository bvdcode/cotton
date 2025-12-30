using Cotton.Storage.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Cotton.Storage.Processors
{
    public class MemoryCacheProcessor : IStorageProcessor
    {
        public int Priority => 10000;

        private const int MaxCacheSize = 100;

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
