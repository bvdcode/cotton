// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO.Compression;
using System.Text;

namespace Cotton.Previews.Tests
{
    public class MeshPreviewGeneratorTests
    {
        [Test]
        public void MeshGenerators_ReportCurrentPreviewVersion()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(new MeshPreviewGenerator().Version, Is.EqualTo(10));
                Assert.That(MeshPreviewGenerator.CreateObjGenerator().Version, Is.EqualTo(10));
                Assert.That(MeshPreviewGenerator.CreateThreeMfGenerator().Version, Is.EqualTo(10));
            }
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_StlInvalidContent_Throws()
        {
            MeshPreviewGenerator generator = new();
            using MemoryStream stream = new MemoryStream(Array.Empty<byte>());

            await Assert.ThrowsAsync<InvalidDataException>(() => generator.GeneratePreviewWebPAsync(stream, size: 128));
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_StlValidModel_RendersWebP()
        {
            MeshPreviewGenerator generator = new();
            using MemoryStream stream = new MemoryStream(CreateValidAsciiStlBytes());

            byte[] preview = await generator.GeneratePreviewWebPAsync(stream, size: 128);

            AssertWebpSignature(preview);
            using Image<Rgba32> image = Image.Load<Rgba32>(preview);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(image.Width, Is.EqualTo(128));
                Assert.That(image.Height, Is.EqualTo(128));
            }
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_ThreeMfWithEmbeddedThumbnail_UsesEmbeddedImage()
        {
            MeshPreviewGenerator generator = MeshPreviewGenerator.CreateThreeMfGenerator();
            byte[] threeMf = CreateThreeMfWithThumbnailBytes(width: 1200, height: 600);
            using MemoryStream stream = new MemoryStream(threeMf);

            byte[] preview = await generator.GeneratePreviewWebPAsync(stream, size: 200);

            AssertWebpSignature(preview);
            using Image<Rgba32> image = Image.Load<Rgba32>(preview);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(image.Width, Is.EqualTo(200));
                Assert.That(image.Height, Is.EqualTo(100));
            }
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_ThreeMfWithoutThumbnailAndInvalidModel_Throws()
        {
            MeshPreviewGenerator generator = MeshPreviewGenerator.CreateThreeMfGenerator();
            byte[] threeMf = CreateThreeMfWithoutThumbnailBytes("not-a-valid-3mf-model");
            using MemoryStream stream = new MemoryStream(threeMf);

            await Assert.ThrowsAsync<InvalidDataException>(() => generator.GeneratePreviewWebPAsync(stream, size: 128));
        }

        [TestCase("stl")]
        [TestCase("obj")]
        [TestCase("3mf")]
        public async Task GeneratePreviewWebPAsync_NonSeekableModel_RendersGeometryAndLeavesSourceOpen(string format)
        {
            IPreviewGenerator generator;
            byte[] model;
            switch (format)
            {
                case "stl":
                    generator = new MeshPreviewGenerator();
                    model = CreateValidAsciiStlBytes();
                    break;
                case "obj":
                    generator = MeshPreviewGenerator.CreateObjGenerator();
                    model = Encoding.ASCII.GetBytes("v 0 0 0\nv 1 0 0\nv 0 1 0\nv 0 0 1\nf 1 3 2\nf 1 2 4\nf 2 3 4\nf 3 1 4\n");
                    break;
                case "3mf":
                    generator = MeshPreviewGenerator.CreateThreeMfGenerator();
                    model = CreateThreeMfWithoutThumbnailBytes(ValidThreeMfModel);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format));
            }

            using MemoryStream compressed = new();
            await using (GZipStream encoder = new(compressed, CompressionMode.Compress, leaveOpen: true))
            {
                await encoder.WriteAsync(model);
            }
            compressed.Position = 0;
            await using GZipStream source = new(compressed, CompressionMode.Decompress);

            byte[] preview = await generator.GeneratePreviewWebPAsync(source, 128);

            using Image<Rgba32> image = Image.Load<Rgba32>(preview);
            int visiblePixels = 0;
            int accentPixels = 0;
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Rgba32 pixel = image[x, y];
                    if (pixel.A > 128)
                    {
                        visiblePixels++;
                        if (pixel.G > pixel.R && pixel.R > pixel.B)
                        {
                            accentPixels++;
                        }
                    }
                }
            }

            Assert.Multiple(() =>
            {
                Assert.That(image.Size, Is.EqualTo(new Size(128, 128)));
                Assert.That(image[0, 0].A, Is.Zero);
                Assert.That(visiblePixels, Is.GreaterThan(1000));
                Assert.That(accentPixels, Is.GreaterThan(visiblePixels / 2));
                Assert.That(source.CanRead, Is.True);
            });
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_SeekableModel_RewindsInput()
        {
            using MemoryStream source = new(CreateValidAsciiStlBytes());
            source.Position = source.Length;

            byte[] preview = await new MeshPreviewGenerator().GeneratePreviewWebPAsync(source, 128);

            AssertWebpSignature(preview);
            Assert.That(source.CanRead, Is.True);
        }

        [Test]
        public async Task GeneratePreviewWebPAsync_ObjInvalidContent_Throws()
        {
            using MemoryStream source = new(Encoding.ASCII.GetBytes("not an OBJ model"));

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                MeshPreviewGenerator.CreateObjGenerator().GeneratePreviewWebPAsync(source, 128));
        }

        private const string ValidThreeMfModel = """
            <?xml version="1.0" encoding="UTF-8"?>
            <model unit="millimeter" xmlns="http://schemas.microsoft.com/3dmanufacturing/core/2015/02">
              <resources>
                <object id="1" type="model"><mesh>
                  <vertices>
                    <vertex x="0" y="0" z="0"/><vertex x="1" y="0" z="0"/>
                    <vertex x="0" y="1" z="0"/><vertex x="0" y="0" z="1"/>
                  </vertices>
                  <triangles>
                    <triangle v1="0" v2="2" v3="1"/><triangle v1="0" v2="1" v3="3"/>
                    <triangle v1="1" v2="2" v3="3"/><triangle v1="2" v2="0" v3="3"/>
                  </triangles>
                </mesh></object>
                <object id="2" type="model"><components>
                  <component objectid="1" transform="1 0 0 0 1 0 0 0 1 3 0 0"/>
                </components></object>
              </resources>
              <build><item objectid="2"/></build>
            </model>
            """;

        private static byte[] CreateValidAsciiStlBytes()
        {
            const string stl = """
        solid cotton_tetrahedron
          facet normal 0 0 -1
            outer loop
              vertex 0 0 0
              vertex 1 0 0
              vertex 0.5 0.866 0
            endloop
          endfacet
          facet normal 0 -0.816 0.577
            outer loop
              vertex 0 0 0
              vertex 0.5 0.866 0
              vertex 0.5 0.289 0.816
            endloop
          endfacet
          facet normal 0.707 0.408 0.577
            outer loop
              vertex 1 0 0
              vertex 0.5 0.289 0.816
              vertex 0.5 0.866 0
            endloop
          endfacet
          facet normal -0.707 0.408 0.577
            outer loop
              vertex 0 0 0
              vertex 0.5 0.289 0.816
              vertex 1 0 0
            endloop
          endfacet
        endsolid cotton_tetrahedron
        """;

            return Encoding.ASCII.GetBytes(stl);
        }

        private static byte[] CreateThreeMfWithThumbnailBytes(int width, int height)
        {
            byte[] thumbnailPng = CreateGradientPngBytes(width, height);

            using MemoryStream output = new MemoryStream();
            using (ZipArchive archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                WriteTextEntry(
                    archive,
                    "[Content_Types].xml",
                    """
                <?xml version="1.0" encoding="UTF-8"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="model" ContentType="application/vnd.ms-package.3dmanufacturing-3dmodel+xml"/>
                  <Default Extension="png" ContentType="image/png"/>
                </Types>
                """);

                WriteTextEntry(
                    archive,
                    "_rels/.rels",
                    """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Target="/3D/3dmodel.model" Id="rel-1" Type="http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel"/>
                  <Relationship Target="/Metadata/thumbnail.png" Id="rel-2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/thumbnail"/>
                </Relationships>
                """);

                WriteTextEntry(
                    archive,
                    "3D/3dmodel.model",
                    """
                <?xml version="1.0" encoding="UTF-8"?>
                <model unit="millimeter" xml:lang="en-US" xmlns="http://schemas.microsoft.com/3dmanufacturing/core/2015/02">
                  <resources></resources>
                  <build></build>
                </model>
                """);

                WriteBinaryEntry(archive, "Metadata/thumbnail.png", thumbnailPng);
            }

            return output.ToArray();
        }

        private static byte[] CreateThreeMfWithoutThumbnailBytes(string modelContent)
        {
            using MemoryStream output = new MemoryStream();
            using (ZipArchive archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                WriteTextEntry(
                    archive,
                    "[Content_Types].xml",
                    """
                <?xml version="1.0" encoding="UTF-8"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="model" ContentType="application/vnd.ms-package.3dmanufacturing-3dmodel+xml"/>
                </Types>
                """);

                WriteTextEntry(
                    archive,
                    "_rels/.rels",
                    """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Target="/3D/3dmodel.model" Id="rel-1" Type="http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel"/>
                </Relationships>
                """);

                WriteTextEntry(archive, "3D/3dmodel.model", modelContent);
            }

            return output.ToArray();
        }

        private static byte[] CreateGradientPngBytes(int width, int height)
        {
            using Image<Rgba32> image = new Image<Rgba32>(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte red = (byte)((x * 255) / Math.Max(1, width - 1));
                    byte green = (byte)((y * 255) / Math.Max(1, height - 1));
                    byte blue = 180;
                    image[x, y] = new Rgba32(red, green, blue, 255);
                }
            }

            using MemoryStream ms = new MemoryStream();
            image.SaveAsPng(ms);
            return ms.ToArray();
        }

        private static void WriteTextEntry(ZipArchive archive, string entryPath, string content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
            using Stream stream = entry.Open();
            byte[] bytes = Encoding.UTF8.GetBytes(content);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteBinaryEntry(ZipArchive archive, string entryPath, byte[] content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
            using Stream stream = entry.Open();
            stream.Write(content, 0, content.Length);
        }

        private static void AssertWebpSignature(byte[] imageBytes)
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(imageBytes, Has.Length.GreaterThanOrEqualTo(12));
                Assert.That(Encoding.ASCII.GetString(imageBytes, 0, 4), Is.EqualTo("RIFF"));
                Assert.That(Encoding.ASCII.GetString(imageBytes, 8, 4), Is.EqualTo("WEBP"));
            }
        }
    }
}
