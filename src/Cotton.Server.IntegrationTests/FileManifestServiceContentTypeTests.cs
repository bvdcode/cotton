// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using Cotton.ContentTypes;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class FileManifestServiceContentTypeTests
    {
        [TestCase("song.ogg", "audio/ogg")]
        [TestCase("SONG.OGG", "audio/ogg")]
        [TestCase("movie.ogv", "video/ogg")]
        [TestCase("script.ts", "text/plain")]
        [TestCase("component.tsx", "text/plain")]
        [TestCase("styles.css", "text/css")]
        [TestCase("index.html", "text/html")]
        [TestCase("script.js", "text/javascript")]
        [TestCase("module.mjs", "text/javascript")]
        [TestCase("README.md", "text/markdown")]
        [TestCase("data.json", "application/json")]
        [TestCase("calendar.ics", "text/calendar")]
        [TestCase("contact.vcf", "text/vcard")]
        [TestCase("notebook.ipynb", "application/x-ipynb+json")]
        [TestCase("document.odt", "application/vnd.oasis.opendocument.text")]
        [TestCase("workbook.ods", "application/vnd.oasis.opendocument.spreadsheet")]
        [TestCase("presentation.odp", "application/vnd.oasis.opendocument.presentation")]
        [TestCase("application.log", "text/plain")]
        [TestCase("document.rst", "text/plain")]
        [TestCase("document.rest", "text/plain")]
        [TestCase("document.adoc", "text/plain")]
        [TestCase("document.asciidoc", "text/plain")]
        [TestCase("document.org", "text/plain")]
        [TestCase("paper.tex", "text/plain")]
        [TestCase("references.bib", "text/plain")]
        [TestCase("application.properties", "text/plain")]
        [TestCase("development.env", "text/plain")]
        [TestCase("lyrics.lrc", "text/plain")]
        [TestCase("captions.srt", "text/plain")]
        [TestCase("captions.vtt", "text/plain")]
        [TestCase("captions.sbv", "text/plain")]
        [TestCase("captions.ass", "text/plain")]
        [TestCase("captions.ssa", "text/plain")]
        [TestCase("Program.cs", "text/plain")]
        [TestCase("Program.java", "text/plain")]
        [TestCase("app.py", "text/plain")]
        [TestCase("Dockerfile", "text/plain")]
        [TestCase("Dockerfile.prod", "text/plain")]
        [TestCase(".dockerignore", "text/plain")]
        [TestCase("photo.heif", "image/heif")]
        [TestCase("song.opus", "audio/opus")]
        [TestCase("model.stl", "model/stl")]
        [TestCase("package.apk", "application/vnd.android.package-archive")]
        [TestCase("file.unknownext", "application/octet-stream")]
        [TestCase("AnimatedSticker.tgs", AnimatedStickerContentTypes.Tgs)]
        [TestCase("opaque-file-name", "application/octet-stream")]
        [TestCase(null, "application/octet-stream")]
        public void ResolveFromFileName_PreservesMediaTypeIndependentlyOfPreviewSupport(
            string? fileName,
            string expectedContentType)
        {
            Assert.That(FileContentTypeResolver.ResolveFromFileName(fileName), Is.EqualTo(expectedContentType));
        }

        [TestCase("index.html")]
        [TestCase("styles.css")]
        [TestCase("script.js")]
        [TestCase("component.jsx")]
        [TestCase("script.sh")]
        [TestCase("data.xml")]
        public void SourceFilename_ResolvesToSupportedTextPreview(string fileName)
        {
            string contentType = FileContentTypeResolver.ResolveFromFileName(fileName);
            Assert.That(Cotton.Previews.PreviewGeneratorProvider.GetGeneratorByContentType(contentType),
                Is.TypeOf<Cotton.Previews.TextPreviewGenerator>());
        }

        [TestCase("IMG_1.heic", null, "image/heic")]
        [TestCase("IMG_1.heif", "application/octet-stream", "image/heif")]
        [TestCase("IMG_1.heics", "application/octet-stream", "image/heic-sequence")]
        [TestCase("IMG_1.heifs", "application/octet-stream", "image/heif-sequence")]
        [TestCase("VID_1.mov", "application/octet-stream", "video/quicktime")]
        [TestCase("VID_1.MOV", "", "video/quicktime")]
        [TestCase("VID_1.mkv", "application/octet-stream", "video/x-matroska")]
        [TestCase("VID_2.mkv", "video/matroska", "video/x-matroska")]
        [TestCase("VID_1.avi", "application/octet-stream", "video/x-msvideo")]
        [TestCase("VID_1.AVI", "", "video/x-msvideo")]
        [TestCase("AUDIO_1.opus", "application/octet-stream", "audio/opus")]
        [TestCase("AUDIO_1.flac", "application/octet-stream", "audio/flac")]
        [TestCase("AUDIO_1.m4b", "application/octet-stream", "audio/mp4")]
        [TestCase("AUDIO_1.mka", "audio/matroska", "audio/x-matroska")]
        [TestCase("README.md", "application/octet-stream", "text/markdown")]
        [TestCase("AnimatedSticker.tgs", "application/gzip", AnimatedStickerContentTypes.Tgs)]
        [TestCase("notebook.ipynb", "application/json", "application/x-ipynb+json")]
        [TestCase("document.odt", "application/zip", "application/vnd.oasis.opendocument.text")]
        [TestCase("workbook.ods", "application/zip", "application/vnd.oasis.opendocument.spreadsheet")]
        [TestCase("presentation.odp", "application/zip", "application/vnd.oasis.opendocument.presentation")]
        [TestCase("Program.cs", "application/octet-stream", "text/plain")]
        [TestCase("Script.csx", "", "text/plain")]
        [TestCase("lyrics.lrc", "application/octet-stream", "text/plain")]
        [TestCase("captions.srt", "application/x-subrip", "text/plain")]
        [TestCase("app.py", "text/x-python", "text/plain")]
        [TestCase("styles.css", "text/css", "text/css")]
        [TestCase("script.ts", "application/x-typescript", "text/plain")]
        [TestCase("Dockerfile", "application/octet-stream", "text/plain")]
        [TestCase(".dockerignore", "", "text/plain")]
        [TestCase("data.json", "application/octet-stream", "application/json")]
        [TestCase("MODEL_1.stl", "application/octet-stream", "model/stl")]
        [TestCase("MODEL_1.obj", "application/octet-stream", "model/obj")]
        [TestCase("MODEL_1.3mf", "application/octet-stream", "model/3mf")]
        [TestCase("MODEL_2.stl", "text/plain", "model/stl")]
        [TestCase("MODEL_2.obj", "application/json", "model/obj")]
        [TestCase("MODEL_2.3mf", "application/zip", "model/3mf")]
        [TestCase("MODEL_3.STL", "text/plain", "model/stl")]
        [TestCase("APP_1.apk", "application/octet-stream", "application/vnd.android.package-archive")]
        [TestCase("APP_2.apk", "application/zip", "application/vnd.android.package-archive")]
        [TestCase("APP_3.APK", "application/zip", "application/vnd.android.package-archive")]
        [TestCase("APP_1.aab", "application/octet-stream", "application/vnd.android.bundle")]
        [TestCase("APP_2.AAB", "application/zip", "application/vnd.android.bundle")]
        [TestCase("APP_1.apks", "application/zip", "application/vnd.android.apks")]
        [TestCase("APP_2.APKS", "application/zip", "application/vnd.android.apks")]
        [TestCase("APP_1.xapk", "application/zip", "application/vnd.android.xapk")]
        [TestCase("APP_2.XAPK", "application/zip", "application/vnd.android.xapk")]
        [TestCase("APP_1.apkm", "application/zip", "application/vnd.android.apkm")]
        [TestCase("APP_2.APKM", "application/zip", "application/vnd.android.apkm")]
        [TestCase("IMG_1.png", "application/octet-stream", "image/png")]
        public void ResolveContentType_OctetStreamOrEmpty_UsesExtensionFallback(
            string fileName,
            string? contentType,
            string expectedContentType)
        {
            string actual = UploadContentTypeResolver.Resolve(fileName, contentType);

            Assert.That(actual, Is.EqualTo(expectedContentType));
        }

        [TestCase("video/mov", "video/quicktime")]
        [TestCase("video/x-quicktime", "video/quicktime")]
        [TestCase("video/vnd.avi", "video/x-msvideo")]
        [TestCase("video/avi", "video/x-msvideo")]
        [TestCase("video/msvideo", "video/x-msvideo")]
        [TestCase("image/x-heic", "image/heic")]
        [TestCase("image/x-heif", "image/heif")]
        [TestCase("audio/x-flac", "audio/flac")]
        [TestCase("audio/x-wav", "audio/wav")]
        [TestCase("application/vnd.ms-pki.stl", "model/stl")]
        [TestCase("application/apk", "application/vnd.android.package-archive")]
        [TestCase("application/x-apk", "application/vnd.android.package-archive")]
        [TestCase("application/x-android-package-archive", "application/vnd.android.package-archive")]
        [TestCase("application/x-android-app-bundle", "application/vnd.android.bundle")]
        [TestCase("application/x-android-apks", "application/vnd.android.apks")]
        [TestCase("application/x-xapk", "application/vnd.android.xapk")]
        [TestCase("application/x-apkm", "application/vnd.android.apkm")]
        [TestCase("application/vnd.apkm", "application/vnd.android.apkm")]
        [TestCase("text/plain; charset=utf-8", "text/plain")]
        [TestCase("APPLICATION/OCTET-STREAM", "application/octet-stream")]
        public void ResolveContentType_NormalizesAliases_AndParameters(string contentType, string expectedContentType)
        {
            string actual = UploadContentTypeResolver.Resolve("sample.bin", contentType);

            Assert.That(actual, Is.EqualTo(expectedContentType));
        }

        [TestCase("Program.cs", true)]
        [TestCase("lyrics.lrc", true)]
        [TestCase("captions.srt", true)]
        [TestCase("captions.vtt", true)]
        [TestCase("captions.sbv", true)]
        [TestCase("captions.ass", true)]
        [TestCase("captions.ssa", true)]
        [TestCase("app.py", true)]
        [TestCase("Dockerfile", true)]
        [TestCase("Dockerfile.prod", true)]
        [TestCase(".dockerignore", true)]
        [TestCase("photo.png", false)]
        [TestCase("archive.zip", false)]
        [TestCase("sample.apk", false)]
        public void IsSourceTextFileName_ClassifiesPreviewableSourceNames(string fileName, bool expected)
        {
            bool actual = FileContentTypeResolver.IsSourceTextFileName(fileName);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void ResolveContentType_UnknownAndEmpty_FallsBackToOctetStream()
        {
            string actual = UploadContentTypeResolver.Resolve("sample.unknownext", null);

            Assert.That(actual, Is.EqualTo(FileContentTypeResolver.DefaultContentType));
        }
    }
}
