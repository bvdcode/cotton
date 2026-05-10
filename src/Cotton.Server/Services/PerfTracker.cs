namespace Cotton.Server.Services
{
    public class PerfTracker
    {
        private const int ChunkTimeoutSeconds = 10;
        private DateTime? _lastChunkCreated;
        private DateTime? _lastPreviewGenerating;

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

        public bool IsNightTime()
        {
            int hour = DateTime.UtcNow.Hour;
            return hour < 6 || hour >= 22;
        }

        public void OnPreviewGenerating()
        {
            _lastPreviewGenerating = DateTime.UtcNow;
        }

        public bool IsPreviewGenerating()
        {
            if (_lastPreviewGenerating == null)
            {
                return false;
            }
            return (DateTime.UtcNow - _lastPreviewGenerating.Value).TotalSeconds < ChunkTimeoutSeconds;
        }
    }
}
