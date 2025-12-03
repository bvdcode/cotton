namespace Cotton.Shared
{
    public record CottonSettings
    {
        public int MaxChunkSizeBytes { get; set; }
        public string MasterEncryptionKey { get; set; } = string.Empty;
        public int MasterEncryptionKeyId { get; set; }
        public int EncryptionThreads { get; set; }
        public int CipherChunkSizeBytes { get; set; }
        public string Pepper { get; set; } = string.Empty;
        public int SessionTimeoutHours { get; set; } = 30 * 24;
    }
}
