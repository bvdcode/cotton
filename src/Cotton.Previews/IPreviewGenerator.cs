namespace Cotton.Previews
{
    public interface IPreviewGenerator
    {
        IEnumerable<string> SupportedContentTypes { get; }
        Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size = PreviewGeneratorProvider.DefaultPreviewSize);
    }
}
