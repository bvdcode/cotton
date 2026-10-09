// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using LibHeifSharp;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Exif.Makernotes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Cotton.Previews.Tests.TestInfrastructure;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.InteropServices;
using System.Text;
using System.Buffers.Binary;
using System.Globalization;

namespace Cotton.Previews.Tests
{
    public class ImageContentMetadataReaderTests
    {
        private const string Xmp = """
            <x:xmpmeta xmlns:x="adobe:ns:meta/">
              <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
                <rdf:Description rdf:about="" xmlns:dc="http://purl.org/dc/elements/1.1/"
                  xmlns:custom="https://example.org/metadata/" custom:unlisted="Custom value">
                  <dc:subject><rdf:Bag><rdf:li>First keyword</rdf:li><rdf:li>Second keyword</rdf:li></rdf:Bag></dc:subject>
                </rdf:Description>
              </rdf:RDF>
            </x:xmpmeta>
            """;

        [Test]
        public async Task ReadAsync_Jpeg_PreservesExifGpsXmpAndIptc()
        {
            using Image<Rgba32> image = new(32, 24);
            image.Metadata.ExifProfile = CreateExif();
            image.Metadata.XmpProfile = new XmpProfile(Encoding.UTF8.GetBytes(Xmp));
            using MemoryStream jpeg = new();
            await image.SaveAsJpegAsync(jpeg);
            using MemoryStream stream = new(AddIptcSegment(jpeg.ToArray()));

            IReadOnlyDictionary<string, string> metadata = await ImageContentMetadataReader.ReadAsync(stream, NullLogger.Instance, CancellationToken.None);

            AssertCommonMetadata(metadata);
            Assert.That(metadata.Values, Does.Contain("IPTC caption"));
            Assert.That(metadata.Keys, Has.Some.Contains("Exposure Time"));
            Assert.That(stream.CanRead, Is.True);
        }

        [Test]
        public async Task ReadAsync_Heic_PreservesExifGpsAndXmp()
        {
            const int size = 32;
            using HeifContext context = new();
            using HeifEncoder encoder = context.GetEncoder(HeifCompressionFormat.Hevc);
            using HeifImage image = new(size, size, HeifColorspace.Rgb, HeifChroma.InterleavedRgb24);
            image.AddPlane(HeifChannel.Interleaved, size, size, 8);
            HeifPlaneData plane = image.GetPlane(HeifChannel.Interleaved);
            byte[] pixels = new byte[plane.Stride * size];
            Marshal.Copy(pixels, 0, plane.Scan0, pixels.Length);
            using HeifImageHandle handle = context.EncodeImageAndReturnHandle(image, encoder, new HeifEncodingOptions());
            context.AddExifMetadata(handle, CreateExif().ToByteArray());
            context.AddXmpMetadata(handle, Encoding.UTF8.GetBytes(Xmp));
            using MemoryStream stream = new();
            context.WriteToStream(stream);
            stream.Position = 0;

            IReadOnlyDictionary<string, string> metadata = await ImageContentMetadataReader.ReadAsync(stream, NullLogger.Instance, CancellationToken.None);

            AssertCommonMetadata(metadata);
            Assert.That(metadata.Keys, Has.Some.Contains("HEIC"));
        }

        [Test]
        public void ReadAsync_Canceled_DoesNotReadSource()
        {
            using MemoryStream stream = new();
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();
            Assert.ThrowsAsync<OperationCanceledException>(
                async () => await ImageContentMetadataReader.ReadAsync(stream, NullLogger.Instance, cancellation.Token));
        }

        [Test]
        public void Flatten_RepeatedDirectoriesAndUnknownTag_PreservesEachValue()
        {
            MetadataExtractor.Formats.Exif.ExifIfd0Directory first = new();
            MetadataExtractor.Formats.Exif.ExifIfd0Directory second = new();
            first.Set(MetadataExtractor.Formats.Exif.ExifDirectoryBase.TagMake, "First camera");
            second.Set(MetadataExtractor.Formats.Exif.ExifDirectoryBase.TagMake, "Second camera");
            second.Set(0xc7ff, "Unknown manufacturer tag");

            IReadOnlyDictionary<string, string> result = ImageContentMetadataReader.Flatten([first, second], NullLogger.Instance, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result["image.Exif IFD0.0.tags.Make.271"], Is.EqualTo("First camera"));
                Assert.That(result["image.Exif IFD0.1.tags.Make.271"], Is.EqualTo("Second camera"));
                Assert.That(result.Values, Does.Contain("Unknown manufacturer tag"));
            });
        }

        [TestCase(16)]
        [TestCase(32)]
        [TestCase(65536)]
        public void Flatten_InvalidOlympusDescription_PreservesRawValuesAndOtherDirectories(int flags)
        {
            OlympusCameraSettingsMakernoteDirectory settings = new();
            settings.Set(OlympusCameraSettingsMakernoteDirectory.TagNoiseReduction, flags);
            settings.Set(0xc7ff, "Other manufacturer value");
            ExifIfd0Directory camera = new();
            camera.Set(ExifDirectoryBase.TagMake, "Camera maker");
            CapturingLogger logger = new();

            IReadOnlyDictionary<string, string> result = ImageContentMetadataReader.Flatten([settings, camera], logger, CancellationToken.None);
            string key = $"image.Olympus Camera Settings.0.tags.Noise Reduction.{OlympusCameraSettingsMakernoteDirectory.TagNoiseReduction}";

            Assert.Multiple(() =>
            {
                Assert.That(result[key], Is.EqualTo(flags.ToString(CultureInfo.InvariantCulture)));
                Assert.That(result, Does.Not.ContainKey($"{key}.description"));
                Assert.That(result.Values, Does.Contain("Other manufacturer value"));
                Assert.That(result["image.Exif IFD0.0.tags.Make.271"], Is.EqualTo("Camera maker"));
                Assert.That(logger.Entries, Has.Count.EqualTo(1));
                Assert.That(logger.Entries[0].Level, Is.EqualTo(LogLevel.Warning));
                Assert.That(logger.Entries[0].Exception, Is.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(logger.Entries[0].Message, Does.Not.Contain("Other manufacturer value"));
            });
        }

        private static ExifProfile CreateExif()
        {
            ExifProfile exif = new();
            exif.SetValue(ExifTag.Make, "Test camera maker");
            exif.SetValue(ExifTag.Model, "Test camera model");
            exif.SetValue(ExifTag.LensModel, "Test lens");
            exif.SetValue(ExifTag.DateTimeOriginal, "2026:10:04 12:34:56");
            exif.SetValue(ExifTag.ExposureTime, new Rational(1, 125));
            exif.SetValue(ExifTag.GPSLatitudeRef, "N");
            exif.SetValue(ExifTag.GPSLatitude, [new Rational(32), new Rational(42), new Rational(30)]);
            exif.SetValue(ExifTag.GPSLongitudeRef, "W");
            exif.SetValue(ExifTag.GPSLongitude, [new Rational(117), new Rational(9), new Rational(0)]);
            return exif;
        }

        private static byte[] AddIptcSegment(byte[] jpeg)
        {
            IptcProfile profile = new();
            profile.SetValue(IptcTag.Caption, "IPTC caption");
            profile.UpdateData();
            byte[] data = profile.Data!;
            using MemoryStream payload = new();
            payload.Write("Photoshop 3.0\0"u8);
            payload.Write("8BIM"u8);
            payload.Write([0x04, 0x04, 0, 0]);
            byte[] length = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
            payload.Write(length);
            payload.Write(data);
            if (data.Length % 2 != 0)
            {
                payload.WriteByte(0);
            }
            using MemoryStream result = new();
            result.Write(jpeg.AsSpan(0, 2));
            result.Write([0xFF, 0xED]);
            BinaryPrimitives.WriteUInt16BigEndian(length, checked((ushort)(payload.Length + 2)));
            result.Write(length.AsSpan(0, 2));
            payload.Position = 0;
            payload.CopyTo(result);
            result.Write(jpeg.AsSpan(2));
            return result.ToArray();
        }

        private static void AssertCommonMetadata(IReadOnlyDictionary<string, string> metadata)
        {
            Assert.Multiple(() =>
            {
                Assert.That(metadata.Values, Does.Contain("Test camera maker"));
                Assert.That(metadata.Values, Does.Contain("Test camera model"));
                Assert.That(metadata.Values, Does.Contain("Test lens"));
                Assert.That(metadata.Values, Does.Contain("1/125"));
                Assert.That(metadata["image.GPS.0.latitude"], Is.EqualTo("32.708333333333336"));
                Assert.That(metadata["image.GPS.0.longitude"], Is.EqualTo("-117.15"));
                Assert.That(metadata.Values, Does.Contain("Custom value"));
                Assert.That(metadata.Values, Does.Contain("First keyword"));
                Assert.That(metadata.Values, Does.Contain("Second keyword"));
                Assert.That(metadata["image.XMP.0.packet"], Does.Contain("custom:unlisted"));
            });
        }
    }
}
