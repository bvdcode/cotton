// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests;

public class FileManifestServiceContentTypeTests
{
    [TestCase("IMG_1.heic", null, "image/heic")]
    [TestCase("IMG_1.heif", "application/octet-stream", "image/heif")]
    [TestCase("IMG_1.heics", "application/octet-stream", "image/heic-sequence")]
    [TestCase("IMG_1.heifs", "application/octet-stream", "image/heif-sequence")]
    [TestCase("VID_1.mov", "application/octet-stream", "video/quicktime")]
    [TestCase("VID_1.MOV", "", "video/quicktime")]
    [TestCase("VID_1.mkv", "application/octet-stream", "video/x-matroska")]
    [TestCase("AUDIO_1.opus", "application/octet-stream", "audio/opus")]
    [TestCase("AUDIO_1.flac", "application/octet-stream", "audio/flac")]
    [TestCase("AUDIO_1.m4b", "application/octet-stream", "audio/mp4")]
    [TestCase("README.md", "application/octet-stream", "text/markdown")]
    [TestCase("MODEL_1.stl", "application/octet-stream", "model/stl")]
    [TestCase("MODEL_1.obj", "application/octet-stream", "model/obj")]
    [TestCase("MODEL_1.3mf", "application/octet-stream", "model/3mf")]
    [TestCase("MODEL_2.stl", "text/plain", "model/stl")]
    [TestCase("MODEL_2.obj", "application/json", "model/obj")]
    [TestCase("MODEL_2.3mf", "application/zip", "model/3mf")]
    [TestCase("IMG_1.png", "application/octet-stream", "image/png")]
    public void ResolveContentType_OctetStreamOrEmpty_UsesExtensionFallback(
        string fileName,
        string? contentType,
        string expectedContentType)
    {
        string actual = FileManifestService.ResolveContentType(fileName, contentType);

        Assert.That(actual, Is.EqualTo(expectedContentType));
    }

    [TestCase("video/mov", "video/quicktime")]
    [TestCase("video/x-quicktime", "video/quicktime")]
    [TestCase("image/x-heic", "image/heic")]
    [TestCase("image/x-heif", "image/heif")]
    [TestCase("audio/x-flac", "audio/flac")]
    [TestCase("audio/x-wav", "audio/wav")]
    [TestCase("application/vnd.ms-pki.stl", "model/stl")]
    [TestCase("text/plain; charset=utf-8", "text/plain")]
    [TestCase("APPLICATION/OCTET-STREAM", "application/octet-stream")]
    public void ResolveContentType_NormalizesAliases_AndParameters(string contentType, string expectedContentType)
    {
        string actual = FileManifestService.ResolveContentType("sample.bin", contentType);

        Assert.That(actual, Is.EqualTo(expectedContentType));
    }

    [Test]
    public void ResolveContentType_UnknownAndEmpty_FallsBackToOctetStream()
    {
        string actual = FileManifestService.ResolveContentType("sample.unknownext", null);

        Assert.That(actual, Is.EqualTo(FileManifestService.DefaultContentType));
    }
}
