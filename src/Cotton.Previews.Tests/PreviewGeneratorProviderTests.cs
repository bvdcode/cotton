// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Previews.Tests
{
    public class PreviewGeneratorProviderTests
    {
        [TestCase("image/png", "image")]
        [TestCase("image/heic", "heic")]
        [TestCase("image/svg+xml", "svg")]
        [TestCase("text/plain", "text")]
        [TestCase("application/pdf", "pdf")]
        [TestCase("audio/mpeg", "audio")]
        [TestCase("video/mp4", "video")]
        [TestCase("application/vnd.android.package-archive", "android-package")]
        [TestCase("model/stl", "stl")]
        [TestCase("model/obj", "obj")]
        [TestCase("model/3mf", "3mf")]
        public void GeneratorIds_AreStableAcrossContentTypes(string contentType, string expectedId)
        {
            IPreviewGenerator generator = PreviewGeneratorProvider.GetGeneratorByContentType(contentType)!;
            Assert.That(generator.Id, Is.EqualTo(expectedId));
            Assert.That(PreviewGeneratorProvider.GetGeneratorVersions()[expectedId], Is.EqualTo(generator.Version));
        }

        [Test]
        public void GetGeneratorsByContentTypes_DeduplicatesAndOrdersByPriority()
        {
            IReadOnlyList<IPreviewGenerator> generators = PreviewGeneratorProvider.GetGeneratorsByContentTypes(
                ["text/plain", "image/png", "IMAGE/PNG", "image/jpeg", "text/css", "application/octet-stream"]);

            Assert.Multiple(() =>
            {
                Assert.That(generators, Has.Count.EqualTo(2));
                Assert.That(generators[0], Is.TypeOf<ImagePreviewGenerator>());
                Assert.That(generators[1], Is.TypeOf<TextPreviewGenerator>());
                Assert.That(generators[0].Priority, Is.LessThan(generators[1].Priority));
            });
        }

        [Test]
        public void GetGeneratorsByContentTypes_KeepsDistinctModelFormats()
        {
            IReadOnlyList<IPreviewGenerator> generators = PreviewGeneratorProvider.GetGeneratorsByContentTypes(
                ["model/stl", "model/obj", "model/3mf", "application/sla"]);

            Assert.That(generators, Has.Count.EqualTo(3));
            Assert.That(generators, Is.Unique);
        }

        [Test]
        public void GetGeneratorsByContentTypes_UnknownTypes_HasNoCandidates()
        {
            Assert.That(PreviewGeneratorProvider.GetGeneratorsByContentTypes(["application/octet-stream", ""]), Is.Empty);
        }

        [TestCase("text/plain", typeof(TextPreviewGenerator))]
        [TestCase("text/x-csharp", typeof(TextPreviewGenerator))]
        [TestCase("application/pdf", typeof(PdfPreviewGenerator))]
        [TestCase("image/heic", typeof(HeicPreviewGenerator))]
        [TestCase("model/stl", typeof(StlThumbPreviewGenerator))]
        [TestCase("application/sla", typeof(StlThumbPreviewGenerator))]
        [TestCase("application/vnd.ms-pki.stl", typeof(StlThumbPreviewGenerator))]
        [TestCase("model/obj", typeof(StlThumbPreviewGenerator))]
        [TestCase("model/3mf", typeof(StlThumbPreviewGenerator))]
        [TestCase("application/vnd.ms-package.3dmanufacturing-3dmodel+xml", typeof(StlThumbPreviewGenerator))]
        [TestCase("video/mp4", typeof(VideoPreviewGenerator))]
        [TestCase("video/vnd.avi", typeof(VideoPreviewGenerator))]
        [TestCase("audio/mpeg", typeof(AudioPreviewGenerator))]
        [TestCase("image/svg+xml", typeof(SvgPreviewGenerator))]
        [TestCase("application/vnd.android.package-archive", typeof(AndroidPackagePreviewGenerator))]
        [TestCase("application/vnd.android.bundle", typeof(AndroidPackagePreviewGenerator))]
        [TestCase("application/vnd.android.apks", typeof(AndroidPackagePreviewGenerator))]
        [TestCase("application/vnd.android.xapk", typeof(AndroidPackagePreviewGenerator))]
        [TestCase("application/vnd.android.apkm", typeof(AndroidPackagePreviewGenerator))]
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
            using (Assert.EnterMultipleScope())
            {
                Assert.That(PreviewGeneratorProvider.GetGeneratorByContentType("application/x-unknown"), Is.Null);
                Assert.That(PreviewGeneratorProvider.GetGeneratorByContentType(string.Empty), Is.Null);
                Assert.That(PreviewGeneratorProvider.GetGeneratorByContentType("   "), Is.Null);
            }
        }

        [Test]
        public void GetAllSupportedMimeTypes_ContainsCriticalTypes_AndHasNoCaseInsensitiveDuplicates()
        {
            string[] mimeTypes = PreviewGeneratorProvider.GetAllSupportedMimeTypes();

            Assert.That(mimeTypes, Is.Not.Empty);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(mimeTypes, Does.Contain("text/plain"));
                Assert.That(mimeTypes, Does.Contain("text/x-csharp"));
                Assert.That(mimeTypes, Does.Contain("application/pdf"));
                Assert.That(mimeTypes, Does.Contain("image/heic"));
                Assert.That(mimeTypes, Does.Contain("image/svg+xml"));
                Assert.That(mimeTypes, Does.Contain("image/png"));
                Assert.That(mimeTypes, Does.Contain("audio/mpeg"));
                Assert.That(mimeTypes, Does.Contain("video/mp4"));
                Assert.That(mimeTypes, Does.Contain("video/vnd.avi"));
                Assert.That(mimeTypes, Does.Contain("application/vnd.android.package-archive"));
                Assert.That(mimeTypes, Does.Contain("application/vnd.android.bundle"));
                Assert.That(mimeTypes, Does.Contain("application/vnd.android.apks"));
                Assert.That(mimeTypes, Does.Contain("application/vnd.android.xapk"));
                Assert.That(mimeTypes, Does.Contain("application/vnd.android.apkm"));
                Assert.That(mimeTypes, Does.Contain("model/stl"));
                Assert.That(mimeTypes, Does.Contain("application/sla"));
                Assert.That(mimeTypes, Does.Contain("application/vnd.ms-pki.stl"));
                Assert.That(mimeTypes, Does.Contain("model/obj"));
                Assert.That(mimeTypes, Does.Contain("model/3mf"));
                Assert.That(mimeTypes, Does.Contain("application/vnd.ms-package.3dmanufacturing-3dmodel+xml"));
            }

            int distinctCount = mimeTypes.Distinct(StringComparer.OrdinalIgnoreCase).Count();
            Assert.That(distinctCount, Is.EqualTo(mimeTypes.Length));
        }

        [Test]
        public void DefaultPreviewSizes_AreStableAndOrdered()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(PreviewGeneratorProvider.DefaultSmallPreviewSize, Is.EqualTo(200));
                Assert.That(PreviewGeneratorProvider.DefaultLargePreviewSize, Is.EqualTo(2560));
                Assert.That(PreviewGeneratorProvider.DefaultLargePreviewSize, Is.GreaterThan(PreviewGeneratorProvider.DefaultSmallPreviewSize));
            }
        }
    }
}
