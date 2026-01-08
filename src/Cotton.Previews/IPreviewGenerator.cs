namespace Cotton.Previews
{
    public interface IPreviewGenerator
    {
        Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size = 256);
    }
}
