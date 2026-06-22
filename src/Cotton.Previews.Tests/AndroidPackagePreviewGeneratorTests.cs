// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

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
    public void Version_ForcesReprocessingAfterDeclaredIconFallbackChange()
    {
        Assert.That(_generator.Version, Is.EqualTo(5));
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
    public async Task GeneratePreviewWebPAsync_ApkWithDeclaredApplicationIcon_UsesManifestResourceEntry()
    {
        const uint iconResourceId = 0x7F010000;
        byte[] source = CreateZipBytes(entries =>
        {
            entries["AndroidManifest.xml"] = CreateBinaryManifestWithApplicationIcon(iconResourceId);
            entries["resources.arsc"] = CreateResourceTableWithIconPaths(
                iconResourceId,
                ("res/declared-mdpi.png", 160),
                ("res/declared-xxxhdpi.png", 640));
            entries["res/mipmap-xxxhdpi/ic_launcher.png"] = CreateSolidPngBytes(192, 192, new Rgba32(230, 30, 30));
            entries["res/declared-mdpi.png"] = CreateSolidPngBytes(48, 48, new Rgba32(30, 60, 220));
            entries["res/declared-xxxhdpi.png"] = CreateSolidPngBytes(192, 192, new Rgba32(30, 220, 80));
        });

        using var stream = new MemoryStream(source);
        byte[] preview = await _generator.GeneratePreviewWebPAsync(stream, size: 96);

        AssertWebpSignature(preview);
        using Image<Rgba32> image = Image.Load<Rgba32>(preview);
        Rgba32 center = image[image.Width / 2, image.Height / 2];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(center.R, Is.LessThan(100));
            Assert.That(center.G, Is.GreaterThan(160));
            Assert.That(center.B, Is.LessThan(130));
        }
    }

    [Test]
    public async Task GeneratePreviewWebPAsync_ApkWithDeclaredApplicationIconWithoutRaster_FallsBackToExplicitLauncherRaster()
    {
        const uint iconResourceId = 0x7F010000;
        byte[] source = CreateZipBytes(entries =>
        {
            entries["AndroidManifest.xml"] = CreateBinaryManifestWithApplicationIcon(iconResourceId);
            entries["resources.arsc"] = CreateResourceTableWithIconPaths(
                iconResourceId,
                ("res/declared.xml", 0xFFFE));
            entries["res/declared.xml"] = Encoding.UTF8.GetBytes("<adaptive-icon />");
            entries["res/mipmap-xxxhdpi/ic_launcher.png"] = CreateSolidPngBytes(192, 192, new Rgba32(230, 30, 30));
        });

        using var stream = new MemoryStream(source);
        byte[] preview = await _generator.GeneratePreviewWebPAsync(stream, size: 96);

        AssertWebpSignature(preview);
        using Image<Rgba32> image = Image.Load<Rgba32>(preview);
        Rgba32 center = image[image.Width / 2, image.Height / 2];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(center.R, Is.GreaterThan(170));
            Assert.That(center.G, Is.LessThan(90));
            Assert.That(center.B, Is.LessThan(90));
        }
    }

    [Test]
    public async Task GeneratePreviewWebPAsync_ApkWithDeclaredApplicationIconWithoutRaster_IgnoresUnrelatedRaster()
    {
        const uint iconResourceId = 0x7F010000;
        byte[] source = CreateZipBytes(entries =>
        {
            entries["AndroidManifest.xml"] = CreateBinaryManifestWithApplicationIcon(iconResourceId);
            entries["resources.arsc"] = CreateResourceTableWithIconPaths(
                iconResourceId,
                ("res/declared.xml", 0xFFFE));
            entries["res/declared.xml"] = Encoding.UTF8.GetBytes("<adaptive-icon />");
            entries["res/drawable/splash.png"] = CreateSolidPngBytes(192, 192, new Rgba32(230, 30, 30));
        });

        using var stream = new MemoryStream(source);
        byte[] preview = await _generator.GeneratePreviewWebPAsync(stream, size: 96);

        AssertWebpSignature(preview);
        using Image<Rgba32> image = Image.Load<Rgba32>(preview);
        Rgba32 center = image[image.Width / 2, image.Height / 2];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(center.R, Is.LessThan(170));
            Assert.That(center.G, Is.GreaterThan(120));
            Assert.That(center.B, Is.LessThan(130));
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

    private static byte[] CreateBinaryManifestWithApplicationIcon(uint iconResourceId)
    {
        byte[] stringPool = CreateStringPool(["manifest", "application", "icon"]);
        byte[] resourceMap = CreateResourceMap([0, 0, 0x01010002]);
        byte[] manifestElement = CreateStartElementChunk(nameIndex: 0);
        byte[] applicationElement = CreateStartElementChunk(
            nameIndex: 1,
            (NameIndex: 2, DataType: 0x01, Data: iconResourceId));

        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        uint size = (uint)(8 + stringPool.Length + resourceMap.Length + manifestElement.Length + applicationElement.Length);
        writer.Write((ushort)0x0003);
        writer.Write((ushort)8);
        writer.Write(size);
        writer.Write(stringPool);
        writer.Write(resourceMap);
        writer.Write(manifestElement);
        writer.Write(applicationElement);
        return output.ToArray();
    }

    private static byte[] CreateResourceTableWithIconPaths(
        uint iconResourceId,
        params (string Path, ushort Density)[] paths)
    {
        byte[] globalStringPool = CreateStringPool(paths.Select(x => x.Path).ToArray());
        byte[] package = CreatePackageChunk(iconResourceId, paths);

        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        writer.Write((ushort)0x0002);
        writer.Write((ushort)12);
        writer.Write((uint)(12 + globalStringPool.Length + package.Length));
        writer.Write((uint)1);
        writer.Write(globalStringPool);
        writer.Write(package);
        return output.ToArray();
    }

    private static byte[] CreatePackageChunk(
        uint iconResourceId,
        IReadOnlyList<(string Path, ushort Density)> paths)
    {
        byte[] typeStringPool = CreateStringPool(["mipmap"]);
        byte[] keyStringPool = CreateStringPool(["ic_app"]);
        byte[][] typeChunks = paths
            .Select((path, index) => CreateTypeChunk(iconResourceId, path.Density, (uint)index))
            .ToArray();

        const ushort headerSize = 288;
        uint typeStringsOffset = headerSize;
        uint keyStringsOffset = typeStringsOffset + (uint)typeStringPool.Length;
        uint size = keyStringsOffset + (uint)keyStringPool.Length + (uint)typeChunks.Sum(x => x.Length);

        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        writer.Write((ushort)0x0200);
        writer.Write(headerSize);
        writer.Write(size);
        writer.Write((uint)((iconResourceId >> 24) & 0xFF));
        writer.Write(new byte[256]);
        writer.Write(typeStringsOffset);
        writer.Write((uint)0);
        writer.Write(keyStringsOffset);
        writer.Write((uint)0);
        writer.Write((uint)0);
        writer.Write(typeStringPool);
        writer.Write(keyStringPool);
        foreach (byte[] typeChunk in typeChunks)
        {
            writer.Write(typeChunk);
        }

        return output.ToArray();
    }

    private static byte[] CreateTypeChunk(uint iconResourceId, ushort density, uint stringIndex)
    {
        const ushort headerSize = 84;
        const uint entryCount = 1;
        const uint entriesStart = headerSize + (entryCount * sizeof(uint));
        const uint chunkSize = entriesStart + 16;

        byte typeId = (byte)((iconResourceId >> 16) & 0xFF);
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        writer.Write((ushort)0x0201);
        writer.Write(headerSize);
        writer.Write(chunkSize);
        writer.Write(typeId);
        writer.Write((byte)0);
        writer.Write((ushort)0);
        writer.Write(entryCount);
        writer.Write(entriesStart);
        byte[] config = new byte[64];
        BitConverter.GetBytes((uint)config.Length).CopyTo(config, 0);
        BitConverter.GetBytes(density).CopyTo(config, 14);
        writer.Write(config);
        writer.Write((uint)0);
        writer.Write((ushort)8);
        writer.Write((ushort)0);
        writer.Write((uint)0);
        writer.Write((ushort)8);
        writer.Write((byte)0);
        writer.Write((byte)0x03);
        writer.Write(stringIndex);
        return output.ToArray();
    }

    private static byte[] CreateStringPool(IReadOnlyList<string> strings)
    {
        byte[][] encodedStrings = strings.Select(EncodeStringPoolString).ToArray();
        uint stringsStart = (uint)(28 + (strings.Count * sizeof(uint)));
        uint size = stringsStart + (uint)encodedStrings.Sum(x => x.Length);

        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        writer.Write((ushort)0x0001);
        writer.Write((ushort)28);
        writer.Write(size);
        writer.Write((uint)strings.Count);
        writer.Write((uint)0);
        writer.Write((uint)0x00000100);
        writer.Write(stringsStart);
        writer.Write((uint)0);

        uint offset = 0;
        foreach (byte[] encodedString in encodedStrings)
        {
            writer.Write(offset);
            offset += (uint)encodedString.Length;
        }

        foreach (byte[] encodedString in encodedStrings)
        {
            writer.Write(encodedString);
        }

        return output.ToArray();
    }

    private static byte[] EncodeStringPoolString(string value)
    {
        byte[] valueBytes = Encoding.UTF8.GetBytes(value);
        using var output = new MemoryStream();
        output.WriteByte((byte)value.Length);
        output.WriteByte((byte)valueBytes.Length);
        output.Write(valueBytes);
        output.WriteByte(0);
        return output.ToArray();
    }

    private static byte[] CreateResourceMap(IReadOnlyList<uint> resourceIds)
    {
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        writer.Write((ushort)0x0180);
        writer.Write((ushort)8);
        writer.Write((uint)(8 + (resourceIds.Count * sizeof(uint))));
        foreach (uint resourceId in resourceIds)
        {
            writer.Write(resourceId);
        }

        return output.ToArray();
    }

    private static byte[] CreateStartElementChunk(
        int nameIndex,
        params (int NameIndex, byte DataType, uint Data)[] attributes)
    {
        const ushort headerSize = 36;
        const ushort attributeSize = 20;
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        writer.Write((ushort)0x0102);
        writer.Write(headerSize);
        writer.Write((uint)(headerSize + (attributes.Length * attributeSize)));
        writer.Write((uint)0);
        writer.Write(0xFFFFFFFF);
        writer.Write(0xFFFFFFFF);
        writer.Write((uint)nameIndex);
        writer.Write((ushort)20);
        writer.Write(attributeSize);
        writer.Write((ushort)attributes.Length);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);

        foreach ((int attributeNameIndex, byte dataType, uint data) in attributes)
        {
            writer.Write(0xFFFFFFFF);
            writer.Write((uint)attributeNameIndex);
            writer.Write(0xFFFFFFFF);
            writer.Write((ushort)8);
            writer.Write((byte)0);
            writer.Write(dataType);
            writer.Write(data);
        }

        return output.ToArray();
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
