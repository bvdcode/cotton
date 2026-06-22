// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Database.Models;
using Cotton.Server.Mappings;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using Mapster;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests;

public class VideoPlaybackResolverTests
{
    [TestCase("video/mp4")]
    [TestCase("video/webm")]
    [TestCase("video/ogg")]
    [TestCase("video/quicktime")]
    public void BrowserNative_KnownTypes_ReturnTrue(string contentType)
    {
        Assert.That(VideoPlaybackResolver.IsBrowserNativeVideo(contentType), Is.True);
    }

    [TestCase("video/x-msvideo")]
    [TestCase("video/x-matroska")]
    [TestCase("video/x-flv")]
    [TestCase("image/png")]
    [TestCase(null)]
    public void BrowserNative_OtherTypes_ReturnFalse(string? contentType)
    {
        Assert.That(VideoPlaybackResolver.IsBrowserNativeVideo(contentType), Is.False);
    }

    [TestCase("application/pdf", true, VideoPlaybackMode.None)]
    [TestCase(null, true, VideoPlaybackMode.None)]
    [TestCase("video/mp4", false, VideoPlaybackMode.Native)]
    [TestCase("video/webm", true, VideoPlaybackMode.Native)]
    [TestCase("video/x-msvideo", true, VideoPlaybackMode.Transcode)]
    [TestCase("video/x-msvideo", false, VideoPlaybackMode.Unsupported)]
    public void Resolve_UsesNativeOrTranscodeMode(
        string? contentType,
        bool hasPreview,
        VideoPlaybackMode expected)
    {
        Assert.That(VideoPlaybackResolver.Resolve(contentType, hasPreview), Is.EqualTo(expected));
    }

    [TestCase("video/mp4", true, false)]
    [TestCase("video/x-msvideo", true, true)]
    [TestCase("video/x-msvideo", false, false)]
    [TestCase("application/pdf", true, false)]
    public void RequiresTranscoding_TruthTable(string? contentType, bool hasPreview, bool expected)
    {
        Assert.That(VideoPlaybackResolver.RequiresTranscoding(contentType, hasPreview), Is.EqualTo(expected));
    }

    [TestCase("video/mp4", true, false)]
    [TestCase("video/webm", true, false)]
    [TestCase("video/ogg", true, false)]
    [TestCase("video/quicktime", true, false)]
    [TestCase("video/x-msvideo", true, true)]
    [TestCase("video/x-msvideo", false, false)]
    [TestCase("video/x-matroska", true, true)]
    [TestCase("application/pdf", true, false)]
    public void MapsterProjection_RequiresVideoTranscoding_AgreesWithResolver(
        string contentType,
        bool hasPreview,
        bool expected)
    {
        MapsterConfig.Register();

        var nodeFile = new NodeFile
        {
            FileManifest = new FileManifest
            {
                ContentType = contentType,
                ProposedContentHash = [4, 5, 6],
                SmallFilePreviewHash = hasPreview ? [1, 2, 3] : null,
            },
        };
        nodeFile.SetName("sample.bin");

        NodeFileManifestDto dto = nodeFile.Adapt<NodeFileManifestDto>();

        Assert.Multiple(() =>
        {
            Assert.That(dto.RequiresVideoTranscoding, Is.EqualTo(expected));
            Assert.That(dto.RequiresVideoTranscoding,
                Is.EqualTo(VideoPlaybackResolver.RequiresTranscoding(contentType, hasPreview)));
        });
    }
}
