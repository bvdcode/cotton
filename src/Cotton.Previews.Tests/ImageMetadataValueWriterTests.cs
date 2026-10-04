// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using MetadataExtractor;
using System.Globalization;
using System.Text.Json;

namespace Cotton.Previews.Tests
{
    public class ImageMetadataValueWriterTests
    {
        [Test]
        public void Write_PreservesArraysBinaryRationalsPrecisionAndEmbeddedNulls()
        {
            CultureInfo original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Dictionary<string, string> result = new();
                ImageMetadataValueWriter.Write(result, "array", new Rational[] { new(1, 3), new(2, 7) }, CancellationToken.None);
                ImageMetadataValueWriter.Write(result, "number", 32.12345678901234, CancellationToken.None);
                ImageMetadataValueWriter.Write(result, "binary", new byte[] { 0, 1, 255 }, CancellationToken.None);
                ImageMetadataValueWriter.Write(result, "text", "before\0after", CancellationToken.None);

                Assert.Multiple(() =>
                {
                    Assert.That(result["array.0"], Is.EqualTo("1/3"));
                    Assert.That(result["array.1"], Is.EqualTo("2/7"));
                    Assert.That(result["array.length"], Is.EqualTo("2"));
                    Assert.That(result["number"], Is.EqualTo("32.12345678901234"));
                    Assert.That(Convert.FromBase64String(result["binary"]), Is.EqualTo(new byte[] { 0, 1, 255 }));
                    Assert.That(result["binary.encoding"], Is.EqualTo("base64"));
                    Assert.That(result["text"].Contains('\0'), Is.False);
                    Assert.That(JsonSerializer.Deserialize<string>(result["text"]), Is.EqualTo("before\0after"));
                });
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }
    }
}
