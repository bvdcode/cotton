namespace Cotton.Previews
{
    public interface IPreviewGenerator
    {
        int Version { get; }
        IEnumerable<string> SupportedContentTypes { get; }
        Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size);
    }
}
