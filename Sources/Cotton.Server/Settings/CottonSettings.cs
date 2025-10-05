namespace Cotton.Server.Settings
{
    public class CottonSettings
    {
        public int ChunkSizeBytes { get; set; }
        public string? MasterEncryptionKey { get; set; }
        public int MasterEncryptionKeyId { get; set; }
        public int? EncryptionThreads { get; set; }
        public int CipherChunkSizeBytes { get; set; }
    }
}
