// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO.Compression;
using System.Text;

namespace Cotton.Previews.Tests;

public class AndroidPackagePreviewGeneratorTests
{
    private AndroidPackagePreviewGenerator _generator = null!;

    [SetUp]
    public void SetUp()
    {
        _generator = new AndroidPackagePreviewGenerator();
    }

    [Test]
    public async Task GeneratePreviewWebPAsync_ApkWithLauncherIcons_UsesBestLauncherIcon()
    {
        byte[] source = CreateZipBytes(entries =>
        {
            entries["res/drawable/icon.png"] = CreateSolidPngBytes(128, 128, new Rgba32(30, 60, 220));
            entries["res/mipmap-mdpi/ic_launcher.png"] = CreateSolidPngBytes(48, 48, new Rgba32(220, 40, 40));
            entries["res/mipmap-xxxhdpi/ic_launcher.png"] = CreateSolidPngBytes(192, 192, new Rgba32(220, 40, 180));
        });

        using var stream = new MemoryStream(source);
        byte[] preview = await _generator.GeneratePreviewWebPAsync(stream, size: 96);

        AssertWebpSignature(preview);
        using Image<Rgba32> image = Image.Load<Rgba32>(preview);
        Rgba32 center = image[image.Width / 2, image.Height / 2];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(image.Width, Is.EqualTo(96));
            Assert.That(image.Height, Is.EqualTo(96));
            Assert.That(center.R, Is.GreaterThan(170));
            Assert.That(center.G, Is.LessThan(110));
            Assert.That(center.B, Is.GreaterThan(120));
        }
    }

    [Test]
    public async Task GeneratePreviewWebPAsync_XapkWithNestedApk_ExtractsNestedIcon()
    {
        byte[] nestedApk = CreateZipBytes(entries =>
        {
            entries["res/mipmap-xhdpi/ic_launcher.png"] = CreateSolidPngBytes(96, 96, new Rgba32(20, 190, 220));
        });
        byte[] source = CreateZipBytes(entries =>
        {
            entries["manifest.json"] = Encoding.UTF8.GetBytes("""{"name":"Sample"}""");
            entries["base.apk"] = nestedApk;
        });

        using var stream = new MemoryStream(source);
        byte[] preview = await _generator.GeneratePreviewWebPAsync(stream, size: 96);

        AssertWebpSignature(preview);
        using Image<Rgba32> image = Image.Load<Rgba32>(preview);
        Rgba32 center = image[image.Width / 2, image.Height / 2];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(center.R, Is.LessThan(90));
            Assert.That(center.G, Is.GreaterThan(140));
            Assert.That(center.B, Is.GreaterThan(160));
        }
    }

    [Test]
    public async Task GeneratePreviewWebPAsync_ObfuscatedExtensionlessResource_ExtractsDecodeableIcon()
    {
        byte[] source = CreateZipBytes(entries =>
        {
            entries["res/-6.xml"] = Encoding.UTF8.GetBytes("<compiled-placeholder />");
            entries["res/kh"] = CreateSolidPngBytes(1280, 720, new Rgba32(20, 20, 20));
            entries["res/yG"] = CreateSolidPngBytes(128, 128, new Rgba32(230, 80, 30));
        });

        using var stream = new MemoryStream(source);
        byte[] preview = await _generator.GeneratePreviewWebPAsync(stream, size: 96);

        AssertWebpSignature(preview);
        using Image<Rgba32> image = Image.Load<Rgba32>(preview);
        Rgba32 center = image[image.Width / 2, image.Height / 2];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(center.R, Is.GreaterThan(170));
            Assert.That(center.G, Is.InRange(40, 130));
            Assert.That(center.B, Is.LessThan(90));
        }
    }

    [Test]
    public async Task GeneratePreviewWebPAsync_NoRasterIcon_ReturnsFallbackWebp()
    {
        byte[] source = CreateZipBytes(entries =>
        {
            entries["AndroidManifest.xml"] = Encoding.UTF8.GetBytes("binary manifest placeholder");
            entries["res/mipmap-anydpi-v26/ic_launcher.xml"] = Encoding.UTF8.GetBytes("<adaptive-icon />");
        });

        using var stream = new MemoryStream(source);
        byte[] preview = await _generator.GeneratePreviewWebPAsync(stream, size: 96);

        AssertWebpSignature(preview);
        using Image<Rgba32> image = Image.Load<Rgba32>(preview);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(image.Width, Is.EqualTo(96));
            Assert.That(image.Height, Is.EqualTo(96));
        }
    }

    [Test]
    public async Task GeneratePreviewWebPAsync_MalformedPackage_ReturnsFallbackWebp()
    {
        byte[] source = Encoding.UTF8.GetBytes("this is not a zip archive");
        using var stream = new MemoryStream(source);

        byte[] preview = await _generator.GeneratePreviewWebPAsync(stream, size: 96);

        AssertWebpSignature(preview);
        using Image<Rgba32> image = Image.Load<Rgba32>(preview);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(image.Width, Is.EqualTo(96));
            Assert.That(image.Height, Is.EqualTo(96));
        }
    }

    [Test]
    public async Task GeneratePreviewWebPAsync_SeekableStream_NotAtStart_StillReadsFromBeginning()
    {
        byte[] source = CreateZipBytes(entries =>
        {
            entries["res/mipmap-hdpi/ic_launcher.png"] = CreateSolidPngBytes(72, 72, new Rgba32(240, 180, 20));
        });

        using var stream = new MemoryStream(source);
        stream.Position = source.Length / 2;
        byte[] preview = await _generator.GeneratePreviewWebPAsync(stream, size: 96);

        AssertWebpSignature(preview);
    }

    private static byte[] CreateZipBytes(Action<Dictionary<string, byte[]>> configure)
    {
        var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        configure(entries);

        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, byte[] bytes) in entries)
            {
                ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                using Stream entryStream = entry.Open();
                entryStream.Write(bytes);
            }
        }

        return output.ToArray();
    }

    private static byte[] CreateSolidPngBytes(int width, int height, Rgba32 color)
    {
        using var image = new Image<Rgba32>(width, height, color);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
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
