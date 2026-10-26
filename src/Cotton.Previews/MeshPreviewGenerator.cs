// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Render.NET.Mesh;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Numerics;
using PixelBuffer = Render.NET.PixelBuffer;

namespace Cotton.Previews
{
    public class MeshPreviewGenerator : IPreviewGenerator
    {
        private const int RenderTimeoutSeconds = 20;
        private const int MaxSourceBytes = 128 * 1024 * 1024;
        private const int MaxSupersampledSize = 2048;
        private readonly string _modelExtension;
        private readonly string[] _supportedContentTypes;

        public string Id { get; }

        public int Version => 10;

        public int Priority => 0;

        public IEnumerable<string> SupportedContentTypes => _supportedContentTypes;

        public MeshPreviewGenerator()
            : this("stl", ".stl", ["model/stl", "application/sla", "application/vnd.ms-pki.stl"])
        {
        }

        private MeshPreviewGenerator(string id, string modelExtension, string[] supportedContentTypes)
        {
            Id = id;
            _modelExtension = modelExtension;
            _supportedContentTypes = supportedContentTypes;
        }

        public static MeshPreviewGenerator CreateObjGenerator() => new("obj", ".obj", ["model/obj"]);

        public static MeshPreviewGenerator CreateThreeMfGenerator() => new(
            "3mf", ".3mf", ["model/3mf", "application/vnd.ms-package.3dmanufacturing-3dmodel+xml"]);

        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(RenderTimeoutSeconds));
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            TriangleMesh mesh;
            if (_modelExtension == ".3mf")
            {
                using MemoryStream source = new();
                await PreviewStream.CopyBoundedAsync(stream, source, MaxSourceBytes, timeout.Token).ConfigureAwait(false);
                ThreeMfThumbnail? thumbnail = await ThreeMfReader.ReadThumbnailAsync(source, cancellationToken: timeout.Token).ConfigureAwait(false);
                if (thumbnail is not null)
                {
                    using MemoryStream imageSource = new(thumbnail.Data.ToArray(), writable: false);
                    return await new ImagePreviewGenerator().GeneratePreviewWebPAsync(imageSource, size).ConfigureAwait(false);
                }

                source.Position = 0;
                mesh = await MeshReader.ReadAsync(source, _modelExtension, timeout.Token).ConfigureAwait(false);
            }
            else
            {
                mesh = await MeshReader.ReadAsync(stream, _modelExtension, timeout.Token).ConfigureAwait(false);
            }

            PixelBuffer pixels = MeshRenderer.Render(mesh, new MeshRenderOptions
            {
                Width = size,
                Height = size,
                Supersampling = size <= MaxSupersampledSize ? 2 : 1,
                BaseColor = new Vector3(
                    PreviewColorPalette.AccentGreenRed / 255f,
                    PreviewColorPalette.AccentGreenGreen / 255f,
                    PreviewColorPalette.AccentGreenBlue / 255f),
            }, timeout.Token);
            using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(pixels.Pixels.Span, pixels.Width, pixels.Height);
            return await ImagePreviewGenerator.EncodeMaxResizedWebpAsync(image, size).ConfigureAwait(false);
        }
    }
}
