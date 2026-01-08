namespace Cotton.Storage.Pipelines
{
    public class PipelineContext
    {
        public long? FileSizeBytes { get; set; }
        public bool StoreInMemoryCache { get; set; }
    }
}