// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO.Compression;
using System.Text;

namespace Cotton.Previews.Tests
{
    internal static class AndroidPackageTestData
    {
        public static byte[] CreateZipBytes(Action<Dictionary<string, byte[]>> configure)
        {
            Dictionary<string, byte[]> entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            configure(entries);

            using MemoryStream output = new MemoryStream();
            using (ZipArchive archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
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

        public static byte[] CreateSolidPngBytes(int width, int height, Rgba32 color)
        {
            using Image<Rgba32> image = new Image<Rgba32>(width, height, color);
            using MemoryStream stream = new MemoryStream();
            image.SaveAsPng(stream);
            return stream.ToArray();
        }

        public static byte[] CreateBinaryManifestWithApplicationIcon(uint iconResourceId)
        {
            byte[] stringPool = CreateStringPool(["manifest", "application", "icon"]);
            byte[] resourceMap = CreateResourceMap([0, 0, 0x01010002]);
            byte[] manifestElement = CreateStartElementChunk(nameIndex: 0);
            byte[] applicationElement = CreateStartElementChunk(
                nameIndex: 1,
                (NameIndex: 2, DataType: 0x01, Data: iconResourceId));

            using MemoryStream output = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
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

        public static byte[] CreateResourceTableWithIconPaths(
            uint iconResourceId,
            params (string Path, ushort Density)[] paths)
        {
            byte[] globalStringPool = CreateStringPool(paths.Select(path => path.Path).ToArray());
            byte[] package = CreatePackageChunk(iconResourceId, paths);

            using MemoryStream output = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
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
            uint size = keyStringsOffset + (uint)keyStringPool.Length + (uint)typeChunks.Sum(chunk => chunk.Length);

            using MemoryStream output = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
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
            using MemoryStream output = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
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
            uint size = stringsStart + (uint)encodedStrings.Sum(encoded => encoded.Length);

            using MemoryStream output = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
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
            using MemoryStream output = new MemoryStream();
            output.WriteByte((byte)value.Length);
            output.WriteByte((byte)valueBytes.Length);
            output.Write(valueBytes);
            output.WriteByte(0);
            return output.ToArray();
        }

        private static byte[] CreateResourceMap(IReadOnlyList<uint> resourceIds)
        {
            using MemoryStream output = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
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
            using MemoryStream output = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
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
    }
}
