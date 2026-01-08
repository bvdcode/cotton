namespace Cotton.Previews
{
    internal class ImagePreviewGenerator : IPreviewGenerator
    {
        public Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size = 256)
        {
            throw new NotImplementedException();
        }
    }
}