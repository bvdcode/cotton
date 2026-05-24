// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using System.Buffers.Binary;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests;

public class StoredZipArchiveWriterTests
{
    [Test]
    public async Task CentralDirectory_WritesZip64ExtraAfterFileName_WhenOnlyOffsetUsesZip64Sentinel()
    {
        const string path = "tiny.txt";
        byte[] pathBytes = Encoding.UTF8.GetBytes(path);
        long zip64Offset = uint.MaxValue;

        Type writerType = typeof(StoredZipArchiveWriter);
        Type planType = writerType.GetNestedType("ZipEntryPlan", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ZIP entry plan type was not found.");
        object plan = Activator.CreateInstance(
            planType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: [path, pathBytes, 12L, false, false, zip64Offset],
            culture: null)
            ?? throw new InvalidOperationException("ZIP entry plan could not be created.");
        planType.GetProperty("CentralExtraLength")?.SetValue(plan, 12L);

        Type writtenType = writerType.GetNestedType("WrittenZipEntry", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Written ZIP entry type was not found.");
        object written = Activator.CreateInstance(
            writtenType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: [plan, 0u],
            culture: null)
            ?? throw new InvalidOperationException("Written ZIP entry could not be created.");

        MethodInfo method = writerType.GetMethod("WriteCentralDirectoryEntryAsync", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Central directory writer was not found.");
        using var destination = new MemoryStream();
        var task = (Task?)method.Invoke(null, [destination, written, CancellationToken.None]);
        Assert.That(task, Is.Not.Null);
        await task!;

        byte[] bytes = destination.ToArray();
        ushort nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(28, 2));
        ushort extraLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(30, 2));

        Assert.Multiple(() =>
        {
            Assert.That(nameLength, Is.EqualTo(pathBytes.Length));
            Assert.That(extraLength, Is.EqualTo(12));
            Assert.That(bytes.AsSpan(46, pathBytes.Length).ToArray(), Is.EqualTo(pathBytes));
            Assert.That(BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(46 + pathBytes.Length, 2)), Is.EqualTo(0x0001));
            Assert.That(BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(48 + pathBytes.Length, 2)), Is.EqualTo(8));
            Assert.That(BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(50 + pathBytes.Length, 8)), Is.EqualTo(zip64Offset));
        });
    }

    [Test]
    public void RequiresZip64CentralDirectoryMetadata_ReturnsFalse_WhenSizeAndOffsetFit()
    {
        bool requiresZip64 = StoredZipArchiveWriter.RequiresZip64CentralDirectoryMetadata(
            (long)uint.MaxValue - 1,
            (long)uint.MaxValue - 1);

        Assert.That(requiresZip64, Is.False);
    }

    [Test]
    public void RequiresZip64CentralDirectoryMetadata_ReturnsTrue_WhenOffsetUsesZip64Sentinel()
    {
        bool requiresZip64 = StoredZipArchiveWriter.RequiresZip64CentralDirectoryMetadata(
            1024,
            uint.MaxValue);

        Assert.That(requiresZip64, Is.True);
    }

    [Test]
    public void RequiresZip64CentralDirectoryMetadata_ReturnsTrue_WhenSizeUsesZip64Sentinel()
    {
        bool requiresZip64 = StoredZipArchiveWriter.RequiresZip64CentralDirectoryMetadata(
            uint.MaxValue,
            1024);

        Assert.That(requiresZip64, Is.True);
    }

    [Test]
    public void RequiresZip64CentralDirectoryMetadata_ReturnsTrue_WhenOnlyOffsetOverflows()
    {
        bool requiresZip64 = StoredZipArchiveWriter.RequiresZip64CentralDirectoryMetadata(
            1024,
            (long)uint.MaxValue + 1);

        Assert.That(requiresZip64, Is.True);
    }

    [Test]
    public void RequiresZip64CentralDirectoryMetadata_ReturnsTrue_WhenSizeOverflows()
    {
        bool requiresZip64 = StoredZipArchiveWriter.RequiresZip64CentralDirectoryMetadata(
            (long)uint.MaxValue + 1,
            1024);

        Assert.That(requiresZip64, Is.True);
    }
}
