// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.ContentTypes;
using Cotton.Previews;
using Cotton.Server.Extensions;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
using Cotton.Storage.Pipelines;

namespace Cotton.Server.Services.Previews
{
    public class FilePreviewRenderer(IStoragePipeline storage, ILogger<FilePreviewRenderer> logger)
    {
        public async Task<RenderedFilePreview?> RenderAsync(FileManifest manifest, CancellationToken cancellationToken)
        {
            IEnumerable<string> contentTypes = manifest.NodeFiles.Select(file => FileContentTypeResolver.ResolveFromFileName(file.Name));
            IReadOnlyList<IPreviewGenerator> generators = PreviewGeneratorProvider.GetGeneratorsByContentTypes(contentTypes);
            List<Exception> failures = [];
            foreach (IPreviewGenerator generator in generators)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    byte[] small = await RenderSizeAsync(manifest, generator, PreviewGeneratorProvider.DefaultSmallPreviewSize);
                    byte[]? large = null;
                    if (generator is ImagePreviewGenerator or HeicPreviewGenerator or SvgPreviewGenerator)
                    {
                        large = await RenderSizeAsync(manifest, generator, PreviewGeneratorProvider.DefaultLargePreviewSize);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    return new RenderedFilePreview(small, large);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Preview attempt for {FileManifestId} failed with generator supporting {ContentTypes}",
                        manifest.Id, string.Join(", ", generator.SupportedContentTypes));
                    failures.Add(exception);
                }
            }

            if (failures.Count > 0)
            {
                throw new AggregateException("All matching preview generators failed.", failures);
            }

            return null;
        }

        private async Task<byte[]> RenderSizeAsync(FileManifest manifest, IPreviewGenerator generator, int size)
        {
            PipelineContext context = new PipelineContext
            {
                FileSizeBytes = manifest.SizeBytes,
                ChunkLengths = manifest.FileManifestChunks.GetChunkLengths()
            };
            await using Stream source = storage.GetBlobStream(manifest.FileManifestChunks.GetChunkHashes(), context);
            return await generator.GeneratePreviewWebPAsync(source, size);
        }
    }
}
