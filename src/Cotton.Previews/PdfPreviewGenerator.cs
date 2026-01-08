namespace Cotton.Previews
{
    internal class PdfPreviewGenerator : IPreviewGenerator
    {
        public Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size = 256)
        {
            throw new NotImplementedException();
        }
    }
}