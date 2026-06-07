// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Buffers;
using System.Security.Cryptography;
using Cotton.Files;
using Cotton.Nodes;
using Cotton.Settings;
using Cotton.Sdk;
using Cotton.Sync.Local;
using Cotton.Sync.State;

namespace Cotton.Sync.Remote;

/// <summary>
/// Synchronizes remote files through Cotton SDK clients.
/// </summary>
public sealed class SdkRemoteFileSynchronizer : IRemoteFileTransferProgressSynchronizer
{
    private const string DefaultContentType = "application/octet-stream";
    private const int MaximumInitialChunkCollectionCapacity = 65_536;
    private readonly ICottonCloudClient _client;
    private readonly SdkRemoteFileSynchronizerOptions _options;
    private readonly Dictionary<string, Guid> _directoryCache = new(StringComparer.OrdinalIgnoreCase);
    private int? _resolvedChunkSizeBytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="SdkRemoteFileSynchronizer" /> class.
    /// </summary>
    public SdkRemoteFileSynchronizer(ICottonCloudClient client, SdkRemoteFileSynchronizerOptions? options = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? new SdkRemoteFileSynchronizerOptions();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_options.DirectoryPageSize);
        if (_options.ChunkSizeBytes.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_options.ChunkSizeBytes.Value);
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_options.MaxConcurrentChunkUploads);
    }

    /// <inheritdoc />
    public async Task<NodeFileManifestDto> UploadFileAsync(
        Guid rootNodeId,
        string relativePath,
        LocalFileSnapshot localFile,
        NodeFileManifestDto? existingRemoteFile = null,
        CancellationToken cancellationToken = default)
    {
        return await UploadFileAsync(
            rootNodeId,
            relativePath,
            localFile,
            existingRemoteFile,
            transferProgress: null,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<NodeFileManifestDto> UploadFileAsync(
        Guid rootNodeId,
        string relativePath,
        LocalFileSnapshot localFile,
        NodeFileManifestDto? existingRemoteFile,
        IProgress<SyncTransferProgress>? transferProgress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(localFile);
        string normalizedPath = SyncPath.Normalize(relativePath);
        Guid parentNodeId = await EnsureParentNodeAsync(rootNodeId, normalizedPath, cancellationToken).ConfigureAwait(false);
        ReportTransfer(
            transferProgress,
            SyncTransferDirection.Upload,
            normalizedPath,
            transferredBytes: 0,
            totalBytes: localFile.SizeBytes);
        UploadedChunks uploadedChunks = await UploadChunksAsync(
            normalizedPath,
            localFile.FullPath,
            localFile.SizeBytes,
            transferProgress,
            cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(localFile.ContentHash)
            && !string.Equals(localFile.ContentHash, uploadedChunks.ContentHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new LocalFileUnavailableException(
                normalizedPath,
                localFile.FullPath,
                "the file changed during upload.");
        }

        var request = new CreateFileFromChunksRequestDto
        {
            NodeId = parentNodeId,
            ChunkHashes = uploadedChunks.ChunkHashes,
            Name = Path.GetFileName(normalizedPath),
            ContentType = ResolveContentType(normalizedPath),
            Hash = uploadedChunks.ContentHash,
            OriginalNodeFileId = existingRemoteFile?.OriginalNodeFileId == Guid.Empty ? existingRemoteFile.Id : existingRemoteFile?.OriginalNodeFileId,
        };

        NodeFileManifestDto uploaded = existingRemoteFile is null
            ? await _client.Files.CreateFromChunksAsync(request, cancellationToken).ConfigureAwait(false)
            : await _client.Files.UpdateContentAsync(
                existingRemoteFile.Id,
                request,
                existingRemoteFile.ETag,
                cancellationToken).ConfigureAwait(false);
        ReportTransfer(
            transferProgress,
            SyncTransferDirection.Upload,
            normalizedPath,
            localFile.SizeBytes,
            localFile.SizeBytes,
            isCompleted: true);
        return uploaded;
    }

    /// <inheritdoc />
    public Task DownloadFileAsync(Guid nodeFileId, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return _client.Files.DownloadContentAsync(nodeFileId, destination, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task DownloadFileAsync(
        Guid nodeFileId,
        string relativePath,
        long? totalBytes,
        Stream destination,
        IProgress<SyncTransferProgress>? transferProgress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        string normalizedPath = SyncPath.Normalize(relativePath);
        ReportTransfer(
            transferProgress,
            SyncTransferDirection.Download,
            normalizedPath,
            transferredBytes: 0,
            totalBytes);
        DownloadTransferProgress? progress = transferProgress is null
            ? null
            : new DownloadTransferProgress(transferProgress, normalizedPath, totalBytes);
        await _client.Files
            .DownloadContentAsync(nodeFileId, destination, progress: progress, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        long completedBytes = totalBytes ?? progress?.LastTransferredBytes ?? 0;
        ReportTransfer(
            transferProgress,
            SyncTransferDirection.Download,
            normalizedPath,
            completedBytes,
            totalBytes,
            isCompleted: true);
    }

    /// <inheritdoc />
    public Task DeleteFileAsync(
        Guid nodeFileId,
        bool skipTrash = false,
        string? expectedETag = null,
        CancellationToken cancellationToken = default)
    {
        return _client.Files.DeleteAsync(nodeFileId, skipTrash, expectedETag, cancellationToken);
    }

    private async Task<UploadedChunks> UploadChunksAsync(
        string relativePath,
        string filePath,
        long totalBytes,
        IProgress<SyncTransferProgress>? transferProgress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        int chunkSize = await GetChunkSizeAsync(cancellationToken).ConfigureAwait(false);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(chunkSize);
        int chunkCollectionCapacity = EstimateChunkCollectionCapacity(totalBytes, chunkSize);
        var chunkHashes = new List<string>(chunkCollectionCapacity);
        var knownChunkHashes = new HashSet<string>(chunkCollectionCapacity, StringComparer.OrdinalIgnoreCase);
        var pendingUploads = new List<Task<int>>(_options.MaxConcurrentChunkUploads);
        long transferredBytes = 0;
        using IncrementalHash contentHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        try
        {
            await using FileStream stream = new(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: Math.Min(chunkSize, 1024 * 128),
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            while (true)
            {
                int read = await ReadChunkAsync(stream, buffer, chunkSize, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                string hash = Convert.ToHexStringLower(SHA256.HashData(buffer.AsSpan(0, read)));
                contentHash.AppendData(buffer, 0, read);
                chunkHashes.Add(hash);
                pendingUploads.Add(CreateChunkUploadTask(hash, buffer, read, knownChunkHashes, cancellationToken));
                if (pendingUploads.Count >= _options.MaxConcurrentChunkUploads)
                {
                    transferredBytes = await FlushPendingChunkUploadsAsync(
                        pendingUploads,
                        transferredBytes,
                        relativePath,
                        totalBytes,
                        transferProgress).ConfigureAwait(false);
                }
            }

            transferredBytes = await FlushPendingChunkUploadsAsync(
                pendingUploads,
                transferredBytes,
                relativePath,
                totalBytes,
                transferProgress).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        if (chunkHashes.Count == 0)
        {
            string emptyHash = Convert.ToHexStringLower(SHA256.HashData(ReadOnlySpan<byte>.Empty));
            await UploadChunkIfMissingAsync(emptyHash, ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false);
            ReportTransfer(
                transferProgress,
                SyncTransferDirection.Upload,
                relativePath,
                transferredBytes: 0,
                totalBytes);
            chunkHashes.Add(emptyHash);
        }

        string fullContentHash = Convert.ToHexStringLower(contentHash.GetHashAndReset());
        return new UploadedChunks(chunkHashes, fullContentHash);
    }

    private Task<int> CreateChunkUploadTask(
        string hash,
        byte[] sourceBuffer,
        int count,
        HashSet<string> knownChunkHashes,
        CancellationToken cancellationToken)
    {
        if (!knownChunkHashes.Add(hash))
        {
            return Task.FromResult(count);
        }

        byte[] chunkBuffer = ArrayPool<byte>.Shared.Rent(count);
        sourceBuffer.AsSpan(0, count).CopyTo(chunkBuffer);
        return UploadChunkIfMissingAsync(hash, chunkBuffer, count, cancellationToken);
    }

    private static async Task<long> FlushPendingChunkUploadsAsync(
        List<Task<int>> pendingUploads,
        long transferredBytes,
        string relativePath,
        long totalBytes,
        IProgress<SyncTransferProgress>? transferProgress)
    {
        if (pendingUploads.Count == 0)
        {
            return transferredBytes;
        }

        int[] completedBytes = await Task.WhenAll(pendingUploads).ConfigureAwait(false);
        pendingUploads.Clear();
        foreach (int bytes in completedBytes)
        {
            transferredBytes += bytes;
            ReportTransfer(
                transferProgress,
                SyncTransferDirection.Upload,
                relativePath,
                transferredBytes,
                totalBytes);
        }

        return transferredBytes;
    }

    private static int EstimateChunkCollectionCapacity(long totalBytes, int chunkSize)
    {
        if (totalBytes <= 0)
        {
            return 0;
        }

        long estimatedChunkCount = ((totalBytes - 1) / chunkSize) + 1;
        return estimatedChunkCount > MaximumInitialChunkCollectionCapacity
            ? MaximumInitialChunkCollectionCapacity
            : (int)estimatedChunkCount;
    }

    private static void ReportTransfer(
        IProgress<SyncTransferProgress>? progress,
        SyncTransferDirection direction,
        string relativePath,
        long transferredBytes,
        long? totalBytes,
        bool isCompleted = false)
    {
        progress?.Report(new SyncTransferProgress(
            direction,
            relativePath,
            transferredBytes,
            totalBytes,
            isCompleted));
    }

    private sealed class DownloadTransferProgress : IProgress<long>
    {
        private readonly IProgress<SyncTransferProgress> _progress;
        private readonly string _relativePath;
        private readonly long? _totalBytes;

        public DownloadTransferProgress(
            IProgress<SyncTransferProgress> progress,
            string relativePath,
            long? totalBytes)
        {
            _progress = progress;
            _relativePath = relativePath;
            _totalBytes = totalBytes;
        }

        public long LastTransferredBytes { get; private set; }

        public void Report(long transferredBytes)
        {
            LastTransferredBytes = transferredBytes;
            ReportTransfer(
                _progress,
                SyncTransferDirection.Download,
                _relativePath,
                transferredBytes,
                _totalBytes);
        }
    }

    private async Task UploadChunkIfMissingAsync(
        string hash,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        if (await _client.Chunks.ExistsAsync(hash, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await using var chunkStream = new MemoryStream(content.ToArray(), writable: false);
        await _client.Chunks.UploadRawAsync(hash, chunkStream, DefaultContentType, cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> UploadChunkIfMissingAsync(
        string hash,
        byte[] buffer,
        int count,
        CancellationToken cancellationToken)
    {
        try
        {
            if (await _client.Chunks.ExistsAsync(hash, cancellationToken).ConfigureAwait(false))
            {
                return count;
            }

            await using var chunkStream = new MemoryStream(buffer, 0, count, writable: false);
            await _client.Chunks.UploadRawAsync(hash, chunkStream, DefaultContentType, cancellationToken).ConfigureAwait(false);
            return count;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task<int> GetChunkSizeAsync(CancellationToken cancellationToken)
    {
        if (_resolvedChunkSizeBytes.HasValue)
        {
            return _resolvedChunkSizeBytes.Value;
        }

        if (_options.ChunkSizeBytes.HasValue)
        {
            _resolvedChunkSizeBytes = _options.ChunkSizeBytes.Value;
            return _resolvedChunkSizeBytes.Value;
        }

        ClientSettingsDto settings = await _client.Settings.GetAsync(cancellationToken).ConfigureAwait(false);
        if (settings.MaxChunkSizeBytes <= 0)
        {
            throw new InvalidOperationException("Server returned an invalid maximum chunk size.");
        }

        _resolvedChunkSizeBytes = settings.MaxChunkSizeBytes;
        return _resolvedChunkSizeBytes.Value;
    }

    private async Task<Guid> EnsureParentNodeAsync(Guid rootNodeId, string relativePath, CancellationToken cancellationToken)
    {
        string[] segments = relativePath.Split('/');
        if (segments.Length == 1)
        {
            return rootNodeId;
        }

        Guid currentNodeId = rootNodeId;
        string currentPath = string.Empty;
        for (int index = 0; index < segments.Length - 1; index++)
        {
            string segment = segments[index];
            currentPath = string.IsNullOrEmpty(currentPath) ? segment : currentPath + "/" + segment;
            string cacheKey = rootNodeId.ToString("D") + ":" + SyncPath.ToKey(currentPath);
            if (_directoryCache.TryGetValue(cacheKey, out Guid cachedNodeId))
            {
                currentNodeId = cachedNodeId;
                continue;
            }

            NodeDto? existing = await FindChildDirectoryAsync(currentNodeId, segment, cancellationToken).ConfigureAwait(false);
            NodeDto node = existing ?? await _client.Nodes.CreateAsync(currentNodeId, segment, cancellationToken).ConfigureAwait(false);
            currentNodeId = node.Id;
            _directoryCache[cacheKey] = currentNodeId;
        }

        return currentNodeId;
    }

    private async Task<NodeDto?> FindChildDirectoryAsync(Guid parentNodeId, string name, CancellationToken cancellationToken)
    {
        int page = 1;
        int loaded = 0;
        while (true)
        {
            NodeContentDto content = await _client.Nodes.GetChildrenAsync(
                parentNodeId,
                page,
                _options.DirectoryPageSize,
                depth: 0,
                cancellationToken).ConfigureAwait(false);
            NodeDto? match = content.Nodes.FirstOrDefault(node => string.Equals(node.Name, name, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }

            int count = content.Nodes.Count + content.Files.Count;
            loaded += count;
            if (count == 0 || loaded >= content.TotalCount)
            {
                return null;
            }

            page++;
        }
    }

    private string ResolveContentType(string relativePath)
    {
        if (_options.ContentTypeResolver is not null)
        {
            return _options.ContentTypeResolver(relativePath);
        }

        string extension = Path.GetExtension(relativePath).ToLowerInvariant();
        return extension switch
        {
            ".css" => "text/css",
            ".csv" => "text/csv",
            ".htm" or ".html" => "text/html",
            ".json" => "application/json",
            ".md" => "text/markdown",
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".svg" => "image/svg+xml",
            ".txt" => "text/plain",
            ".webp" => "image/webp",
            ".xml" => "application/xml",
            _ => DefaultContentType,
        };
    }

    private static async Task<int> ReadChunkAsync(FileStream stream, byte[] buffer, int chunkSize, CancellationToken cancellationToken)
    {
        int total = 0;
        while (total < chunkSize)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(total, chunkSize - total), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    private sealed record UploadedChunks(List<string> ChunkHashes, string ContentHash);
}
