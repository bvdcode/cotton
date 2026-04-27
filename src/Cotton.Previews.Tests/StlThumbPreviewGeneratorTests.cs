// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Cotton.Previews.Tests;

public class StlThumbPreviewGeneratorTests
{
    [Test]
    public void NativeRenderImport_ModelFilenameParameter_UsesUtf8Marshalling()
    {
        Type nativeType = typeof(StlThumbPreviewGenerator)
            .GetNestedType("StlThumbNative", BindingFlags.NonPublic)
            ?? throw new AssertionException("StlThumbNative nested type was not found.");

        MethodInfo renderMethod = nativeType.GetMethod(
                "RenderToBuffer",
                BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new AssertionException("RenderToBuffer method was not found.");

        ParameterInfo modelFilenameParameter = renderMethod.GetParameters()[3];
        MarshalAsAttribute marshalAs = modelFilenameParameter.GetCustomAttribute<MarshalAsAttribute>()
            ?? throw new AssertionException("MarshalAsAttribute is missing on modelFilename parameter.");

        Assert.That(marshalAs.Value, Is.EqualTo(UnmanagedType.LPUTF8Str));
    }

    [Test]
    public async Task GeneratePreviewWebPAsync_ThreeMfWithEmbeddedThumbnail_UsesEmbeddedImage()
    {
        StlThumbPreviewGenerator generator = StlThumbPreviewGenerator.CreateThreeMfGenerator();
        byte[] threeMf = CreateThreeMfWithThumbnailBytes(width: 1200, height: 600);
        using var stream = new MemoryStream(threeMf);

        byte[] preview = await generator.GeneratePreviewWebPAsync(stream, size: 200);

        AssertWebpSignature(preview);
        using var image = Image.Load<Rgba32>(preview);
        Assert.Multiple(() =>
        {
            Assert.That(image.Width, Is.EqualTo(200));
            Assert.That(image.Height, Is.EqualTo(100));
        });
    }

    private static byte[] CreateThreeMfWithThumbnailBytes(int width, int height)
    {
        byte[] thumbnailPng = CreateGradientPngBytes(width, height);

        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
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

    private static byte[] CreateGradientPngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);

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

        using var ms = new MemoryStream();
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
        Assert.That(imageBytes.Length, Is.GreaterThanOrEqualTo(12));
        Assert.That(Encoding.ASCII.GetString(imageBytes, 0, 4), Is.EqualTo("RIFF"));
        Assert.That(Encoding.ASCII.GetString(imageBytes, 8, 4), Is.EqualTo("WEBP"));
    }
}
