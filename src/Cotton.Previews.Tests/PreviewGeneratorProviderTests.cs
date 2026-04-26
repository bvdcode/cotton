// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Previews;
using NUnit.Framework;

namespace Cotton.Previews.Tests;

public class PreviewGeneratorProviderTests
{
    [TestCase("text/plain", typeof(TextPreviewGenerator))]
    [TestCase("application/pdf", typeof(PdfPreviewGenerator))]
    [TestCase("image/heic", typeof(HeicPreviewGenerator))]
    [TestCase("video/mp4", typeof(VideoPreviewGenerator))]
    [TestCase("audio/mpeg", typeof(AudioPreviewGenerator))]
    [TestCase("image/png", typeof(ImagePreviewGenerator))]
    public void GetGeneratorByContentType_KnownTypes_ReturnsExpectedGenerator(string contentType, Type expectedType)
    {
        IPreviewGenerator? generator = PreviewGeneratorProvider.GetGeneratorByContentType(contentType);

        Assert.That(generator, Is.Not.Null);
        Assert.That(generator, Is.InstanceOf(expectedType));
    }

    [Test]
    public void GetGeneratorByContentType_IsCaseInsensitive()
    {
        IPreviewGenerator? generator = PreviewGeneratorProvider.GetGeneratorByContentType("TEXT/PLAIN");

        Assert.That(generator, Is.Not.Null);
        Assert.That(generator, Is.InstanceOf<TextPreviewGenerator>());
    }

    [Test]
    public void GetGeneratorByContentType_UnknownOrBlank_ReturnsNull()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PreviewGeneratorProvider.GetGeneratorByContentType("application/x-unknown"), Is.Null);
            Assert.That(PreviewGeneratorProvider.GetGeneratorByContentType(string.Empty), Is.Null);
            Assert.That(PreviewGeneratorProvider.GetGeneratorByContentType("   "), Is.Null);
        });
    }

    [Test]
    public void GetAllSupportedMimeTypes_ContainsCriticalTypes_AndHasNoCaseInsensitiveDuplicates()
    {
        string[] mimeTypes = PreviewGeneratorProvider.GetAllSupportedMimeTypes();

        Assert.That(mimeTypes, Is.Not.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(mimeTypes, Does.Contain("text/plain"));
            Assert.That(mimeTypes, Does.Contain("application/pdf"));
            Assert.That(mimeTypes, Does.Contain("image/heic"));
            Assert.That(mimeTypes, Does.Contain("image/png"));
            Assert.That(mimeTypes, Does.Contain("audio/mpeg"));
            Assert.That(mimeTypes, Does.Contain("video/mp4"));
        });

        int distinctCount = mimeTypes.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        Assert.That(distinctCount, Is.EqualTo(mimeTypes.Length));
    }

    [Test]
    public void DefaultPreviewSizes_AreStableAndOrdered()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PreviewGeneratorProvider.DefaultSmallPreviewSize, Is.EqualTo(200));
            Assert.That(PreviewGeneratorProvider.DefaultLargePreviewSize, Is.EqualTo(1600));
            Assert.That(PreviewGeneratorProvider.DefaultLargePreviewSize, Is.GreaterThan(PreviewGeneratorProvider.DefaultSmallPreviewSize));
        });
    }
}
