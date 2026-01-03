namespace Cotton.Server.Services
{
    public class PerfTracker
    {
        private const int ChunkTimeoutSeconds = 10;
        private DateTime? _lastChunkCreated;

        public void OnChunkCreated()
        {
            _lastChunkCreated = DateTime.UtcNow;
        }

        public bool IsUploading()
        {
            if (_lastChunkCreated == null)
            {
                return false;
            }
            return (DateTime.UtcNow - _lastChunkCreated.Value).TotalSeconds < ChunkTimeoutSeconds;
        }
    }
}
