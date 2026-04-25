using Cotton.Database.Models;

namespace Cotton.Server.Models.Dto
{
    public class ServerSettingsEnvelopeDto
    {
        public int MaxChunkSizeBytes { get; init; }
        public string SupportedHashAlgorithm { get; init; } = null!;
        public CottonServerSettings? Settings { get; init; }
    }
}
