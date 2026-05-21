// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests;

public class StoredZipArchiveWriterTests
{
    [Test]
    public void RequiresZip64CentralDirectoryMetadata_ReturnsFalse_WhenSizeAndOffsetFit()
    {
        bool requiresZip64 = StoredZipArchiveWriter.RequiresZip64CentralDirectoryMetadata(
            uint.MaxValue,
            uint.MaxValue);

        Assert.That(requiresZip64, Is.False);
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
