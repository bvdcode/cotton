using System;
using System.Collections.Generic;
using System.Text;

namespace Cotton.Previews
{
    public static class PreviewGeneratorProvider
    {
        public static IPreviewGenerator? GetGeneratorByContentType(string contentType)
        {
            return contentType.ToLowerInvariant() switch
            {
                "image/jpeg" or "image/jpg" or 
                "image/png" or "image/gif" or 
                "image/bmp" or "image/tiff" or 
                "image/webp" or "image/avif" =>
                    new ImagePreviewGenerator(),
                "application/pdf" =>
                    new PdfPreviewGenerator(),
                "text/plain" =>
                    new TextPreviewGenerator(),
                _ => null,
            };
        }
    }
}
