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
        public async Task ReadAsync_Jpeg_PreservesPhotographyAndGpsWithoutXmpAndIptc()
        {
            using Image<Rgba32> image = new(32, 24);
            image.Metadata.ExifProfile = CreateExif();
            image.Metadata.XmpProfile = new XmpProfile(Encoding.UTF8.GetBytes(Xmp));
            using MemoryStream jpeg = new();
            await image.SaveAsJpegAsync(jpeg);
            using MemoryStream stream = new(AddIptcSegment(jpeg.ToArray()));

            IReadOnlyDictionary<string, string> metadata = await ImageContentMetadataReader.ReadAsync(stream, NullLogger.Instance, CancellationToken.None);

            AssertCommonMetadata(metadata);
            Assert.That(metadata.Values, Does.Not.Contain("IPTC caption"));
            Assert.That(metadata["image.width"], Is.EqualTo("32"));
            Assert.That(metadata["image.height"], Is.EqualTo("24"));
            Assert.That(metadata.Keys, Has.Some.Contains("Exposure Time"));
            Assert.That(stream.CanRead, Is.True);
        }

        [Test]
        public async Task ReadAsync_Heic_PreservesPhotographyGpsAndDimensionsWithoutXmp()
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
            Assert.That(metadata["image.width"], Is.EqualTo(size.ToString()));
            Assert.That(metadata["image.height"], Is.EqualTo(size.ToString()));
        }

        [Test]
        public async Task ReadAsync_Canceled_DoesNotReadSource()
        {
            using MemoryStream stream = new();
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await ImageContentMetadataReader.ReadAsync(stream, NullLogger.Instance, cancellation.Token));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task ReadAsync_TruncatedJpeg_ReportsIncompleteMetadata(bool truncatedSegment)
        {
            byte[] bytes = truncatedSegment
                ? [0xFF, 0xD8, 0xFF, 0xE1, 0x00, 0x10, 0x45, 0x78, 0x69, 0x66, 0x00, 0x00]
                : [0xFF, 0xD8, 0xFF, 0xFE, 0x00, 0x02];
            using MemoryStream stream = new(bytes);

            TruncatedImageMetadataException? error = await Assert.ThrowsAsync<TruncatedImageMetadataException>(
                async () => await ImageContentMetadataReader.ReadAsync(stream, NullLogger.Instance, CancellationToken.None));

            Assert.That(error?.InnerException, Is.TypeOf<IOException>());
        }

        [Test]
        public async Task ReadAsync_SourceReadFailure_PreservesIOException()
        {
            IOException failure = new("End of data reached.");
            using FailingCopyStream stream = new(failure);

            IOException? error = await Assert.ThrowsAsync<IOException>(
                async () => await ImageContentMetadataReader.ReadAsync(stream, NullLogger.Instance, CancellationToken.None));

            Assert.That(error, Is.SameAs(failure));
        }

        [Test]
        public async Task ReadAsync_CanceledWhileCopying_PreservesCancellation()
        {
            using BlockingReadStream stream = new(32);
            using CancellationTokenSource cancellation = new();
            Task<IReadOnlyDictionary<string, string>> reading = ImageContentMetadataReader.ReadAsync(stream, NullLogger.Instance, cancellation.Token);
            await stream.ReadStarted.WaitAsync(TimeSpan.FromSeconds(5));
            await cancellation.CancelAsync();

            await Assert.ThrowsAsync<TaskCanceledException>(async () => await reading);
        }

        [Test]
        public void Flatten_RepeatedDirectoriesAndUnknownTag_KeepsOnlyPrimaryPhotography()
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
                Assert.That(result.Keys, Has.None.Contains("image.Exif IFD0.1."));
                Assert.That(result.Values, Does.Not.Contain("Unknown manufacturer tag"));
            });
        }

        [TestCase(16)]
        [TestCase(32)]
        [TestCase(65536)]
        public void Flatten_InvalidOlympusDescription_DoesNotReadDiscardedManufacturerTags(int flags)
        {
            OlympusCameraSettingsMakernoteDirectory settings = new();
            settings.Set(OlympusCameraSettingsMakernoteDirectory.TagNoiseReduction, flags);
            settings.Set(0xc7ff, "Other manufacturer value");
            ExifIfd0Directory camera = new();
            camera.Set(ExifDirectoryBase.TagMake, "Camera maker");
            CapturingLogger logger = new();

            IReadOnlyDictionary<string, string> result = ImageContentMetadataReader.Flatten([settings, camera], logger, CancellationToken.None);
            Assert.Multiple(() =>
            {
                Assert.That(result.Keys, Has.None.Contains("Olympus"));
                Assert.That(result.Values, Does.Not.Contain("Other manufacturer value"));
                Assert.That(result["image.Exif IFD0.0.tags.Make.271"], Is.EqualTo("Camera maker"));
                Assert.That(logger.Entries, Is.Empty);
            });
        }

        [Test]
        public void Flatten_RawSubDirectories_MergesPhotographyAndKeepsFullImageDimensions()
        {
            ExifSubIfdDirectory thumbnail = new();
            thumbnail.Set(ExifDirectoryBase.TagImageWidth, 160);
            thumbnail.Set(ExifDirectoryBase.TagImageHeight, 120);
            ExifSubIfdDirectory photograph = new();
            photograph.Set(ExifDirectoryBase.TagExifImageWidth, 4000);
            photograph.Set(ExifDirectoryBase.TagExifImageHeight, 3000);
            photograph.Set(ExifDirectoryBase.TagExposureTime, new MetadataExtractor.Rational(1, 125));
            photograph.Set(ExifDirectoryBase.TagIsoEquivalent, 400);

            IReadOnlyDictionary<string, string> result = ImageContentMetadataReader.Flatten([thumbnail, photograph], NullLogger.Instance, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result["image.width"], Is.EqualTo("4000"));
                Assert.That(result["image.height"], Is.EqualTo("3000"));
                Assert.That(result["image.Exif SubIFD.0.tags.Exposure Time.33434"], Is.EqualTo("1/125"));
                Assert.That(result.Values, Does.Contain("400"));
                Assert.That(result.Keys, Has.None.Contains("image.Exif SubIFD.1."));
            });
        }

        [Test]
        public void Flatten_LargePrivatePayloads_DoesNotSerializeBinaryArraysOrCopyright()
        {
            ExifIfd0Directory camera = new();
            camera.Set(ExifDirectoryBase.TagMake, "Camera maker");
            camera.Set(ExifDirectoryBase.TagCopyright, new string('x', 100000));
            ExifSubIfdDirectory exposure = new();
            exposure.Set(ExifDirectoryBase.TagExposureTime, new MetadataExtractor.Rational(1, 125));
            exposure.Set(ExifDirectoryBase.TagFNumber, new MetadataExtractor.Rational(28, 10));
            exposure.Set(ExifDirectoryBase.TagIsoEquivalent, 400);
            exposure.Set(ExifDirectoryBase.TagMakernote, new byte[1000000]);
            exposure.Set(0xc7ff, new int[100000]);

            IReadOnlyDictionary<string, string> result = ImageContentMetadataReader.Flatten([camera, exposure], NullLogger.Instance, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.Values, Does.Contain("Camera maker"));
                Assert.That(result.Values, Does.Contain("1/125"));
                Assert.That(result.Values, Does.Contain("f/2.8"));
                Assert.That(result.Values, Does.Contain("400"));
                Assert.That(result, Has.Count.LessThanOrEqualTo(8));
                Assert.That(result.Sum(entry => entry.Key.Length + entry.Value.Length), Is.LessThan(1000));
                Assert.That(result.Keys, Has.None.Contains("Copyright"));
                Assert.That(result.Keys, Has.None.Contains("Makernote"));
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
                Assert.That(metadata.Keys, Has.None.StartsWith("image.XMP."));
                Assert.That(metadata.Keys, Has.None.StartsWith("image.Photoshop."));
                Assert.That(metadata.Keys, Has.None.EndsWith(".bytes"));
            });
        }
    }
}
