// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Cotton.Server.Controllers;

/// <summary>
/// Development-only anonymous upload benchmark endpoint.
/// </summary>
[ApiController]
[Route(Routes.V1.Base + "/debug/benchmark")]
public sealed class DebugBenchmarkController : ControllerBase
{
    private const int MinMaxMebibytes = 1;
    private const int MaxMaxMebibytes = 4096;
    private const int DefaultMaxMebibytes = 1024;
    private const int MinBufferKibibytes = 4;
    private const int MaxBufferKibibytes = 4096;
    private const int DefaultBufferKibibytes = 1024;
    private const double BytesPerMebibyte = 1024.0 * 1024.0;

    /// <summary>
    /// Runs a disposable upload benchmark against discard or temp-file targets.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> Run(
        [FromQuery] string target = "discard",
        [FromQuery] bool hash = true,
        [FromQuery] int maxMiB = DefaultMaxMebibytes,
        [FromQuery] int bufferKiB = DefaultBufferKibibytes,
        CancellationToken cancellationToken = default)
    {
        // DEVELOPMENT ONLY: this unauthenticated workload endpoint is intentionally dirty and isolated.
        // Delete this controller before merging diagnostics into a release/main branch.
        if (!TryParseTarget(target, out DebugBenchmarkTarget parsedTarget))
        {
            return BadRequest(new { error = "target must be one of: discard, temp." });
        }

        if (maxMiB is < MinMaxMebibytes or > MaxMaxMebibytes)
        {
            return BadRequest(new { error = $"maxMiB must be between {MinMaxMebibytes} and {MaxMaxMebibytes}." });
        }

        if (bufferKiB is < MinBufferKibibytes or > MaxBufferKibibytes)
        {
            return BadRequest(new { error = $"bufferKiB must be between {MinBufferKibibytes} and {MaxBufferKibibytes}." });
        }

        long maxBytes = (long)maxMiB * 1024 * 1024;
        if (Request.ContentLength is > 0 && Request.ContentLength > maxBytes)
        {
            return BadRequest(new { error = $"Request body exceeds the configured {maxMiB} MiB diagnostic limit." });
        }

        string? tempFilePath = null;
        var stopwatch = Stopwatch.StartNew();

        await using var input = new CountingHashingReadStream(
            Request.Body,
            hash,
            maxBytes,
            bufferKiB * 1024);

        try
        {
            switch (parsedTarget)
            {
                case DebugBenchmarkTarget.Discard:
                    await input.CopyToAsync(Stream.Null, input.BufferSize, cancellationToken);
                    break;
                case DebugBenchmarkTarget.Temp:
                    tempFilePath = CreateTempFilePath();
                    await using (var temp = new FileStream(tempFilePath, new FileStreamOptions
                    {
                        Mode = FileMode.CreateNew,
                        Access = FileAccess.Write,
                        Share = FileShare.None,
                        BufferSize = input.BufferSize,
                        Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                    }))
                    {
                        await input.CopyToAsync(temp, input.BufferSize, cancellationToken);
                        await temp.FlushAsync(cancellationToken);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported benchmark target.");
            }

            string? sha256 = input.GetHashHex();
            stopwatch.Stop();
            return Ok(new
            {
                completedAt = DateTimeOffset.UtcNow,
                target = parsedTarget.ToString().ToLowerInvariant(),
                hash,
                requestContentLength = Request.ContentLength,
                bytesRead = input.BytesRead,
                elapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
                mebibytesPerSecond = ToMebibytesPerSecond(input.BytesRead, stopwatch.Elapsed),
                sha256,
                tempFilePath,
                tempFileDeleted = tempFilePath is not null,
                bufferBytes = input.BufferSize,
                maxBytes,
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Debug benchmark", StringComparison.Ordinal))
        {
            return BadRequest(new { error = ex.Message });
        }
        finally
        {
            if (tempFilePath is not null)
            {
                TryDeleteTempFile(tempFilePath);
            }
        }
    }

    private static string CreateTempFilePath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "cotton-debug-benchmark");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
    }

    private static void TryDeleteTempFile(string path)
    {
        try
        {
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
        catch
        {
            // Dirty development benchmark: cleanup failure is intentionally ignored.
        }
    }

    private static bool TryParseTarget(string target, out DebugBenchmarkTarget parsedTarget)
    {
        return Enum.TryParse(target, ignoreCase: true, out parsedTarget)
            && Enum.IsDefined(parsedTarget);
    }

    private static double ToMebibytesPerSecond(long bytes, TimeSpan elapsed)
    {
        return elapsed.TotalSeconds <= 0
            ? 0
            : bytes / BytesPerMebibyte / elapsed.TotalSeconds;
    }

    private enum DebugBenchmarkTarget
    {
        Discard,
        Temp,
    }

    private sealed class CountingHashingReadStream(
        Stream inner,
        bool hash,
        long maxBytes,
        int bufferSize) : Stream
    {
        private readonly IncrementalHash? _hasher = hash ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256) : null;
        private bool _hashFinalized;

        public long BytesRead { get; private set; }
        public int BufferSize { get; } = bufferSize;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int read = await inner.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return 0;
            }

            BytesRead += read;
            if (BytesRead > maxBytes)
            {
                throw new InvalidOperationException("Debug benchmark request body exceeded the configured limit.");
            }

            _hasher?.AppendData(buffer.Span[..read]);
            return read;
        }

        public string? GetHashHex()
        {
            if (_hasher is null)
            {
                return null;
            }

            if (_hashFinalized)
            {
                throw new InvalidOperationException("Hash was already finalized.");
            }

            _hashFinalized = true;
            string hash = Convert.ToHexString(_hasher.GetHashAndReset()).ToLowerInvariant();
            _hasher.Dispose();
            return hash;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_hashFinalized)
            {
                _hasher?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
