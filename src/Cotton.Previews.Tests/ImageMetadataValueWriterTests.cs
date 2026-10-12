// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using MetadataExtractor;
using System.Globalization;
using System.Text.Json;
using System.Text;

namespace Cotton.Previews.Tests
{
    public class ImageMetadataValueWriterTests
    {
        [Test]
        public void Write_PreservesScalarsAndEmbeddedNullsWithoutBinaryOrArrayExpansion()
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
                    Assert.That(result.Keys, Has.None.StartsWith("array"));
                    Assert.That(result["number"], Is.EqualTo("32.12345678901234"));
                    Assert.That(result.Keys, Has.None.StartsWith("binary"));
                    Assert.That(result["text"].Contains('\0'), Is.False);
                    Assert.That(JsonSerializer.Deserialize<string>(result["text"]), Is.EqualTo("before\0after"));
                });
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        [Test]
        public void Write_LargeTextAndStringValue_DoesNotRetainPayloadOrDuplicateBytes()
        {
            Dictionary<string, string> result = new();
            ImageMetadataValueWriter.Write(result, "large", new string('x', 100000), CancellationToken.None);
            ImageMetadataValueWriter.Write(result, "text", new StringValue("Camera"u8.ToArray(), Encoding.UTF8), CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Not.ContainKey("large"));
                Assert.That(result["text"], Is.EqualTo("Camera"));
                Assert.That(result, Does.Not.ContainKey("text.bytes"));
            });
        }
    }
}
