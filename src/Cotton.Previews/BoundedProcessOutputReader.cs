// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Buffers;
using System.Text;

namespace Cotton.Previews
{
    /// <summary>
    /// Captures process output up to a byte limit while continuing to drain the source stream.
    /// </summary>
    internal class BoundedProcessOutputReader
    {
        private const int ReadBufferSize = 8 * 1024;
        private readonly int _maxBytes;
        private readonly string _outputName;
        private readonly TaskCompletionSource<FfprobeOutputLimitExceededException> _limitExceeded =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BoundedProcessOutputReader(string outputName, int maxBytes)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outputName);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

            _outputName = outputName;
            _maxBytes = maxBytes;
        }

        public Task<FfprobeOutputLimitExceededException> LimitExceeded => _limitExceeded.Task;

        public async Task<string> ReadAsync(Stream source, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(source);

            int initialCapacity = Math.Min(_maxBytes, ReadBufferSize);
            using MemoryStream captured = new(initialCapacity);
            byte[] readBuffer = ArrayPool<byte>.Shared.Rent(ReadBufferSize);
            bool exceeded = false;

            try
            {
                while (true)
                {
                    int read = await source
                        .ReadAsync(readBuffer.AsMemory(0, readBuffer.Length), cancellationToken)
                        .ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    if (exceeded)
                    {
                        continue;
                    }

                    int remainingCapacity = _maxBytes - checked((int)captured.Length);
                    int bytesToCapture = Math.Min(read, remainingCapacity);
                    if (bytesToCapture > 0)
                    {
                        captured.Write(readBuffer, 0, bytesToCapture);
                    }

                    if (bytesToCapture < read)
                    {
                        exceeded = true;
                        _limitExceeded.TrySetResult(
                            new FfprobeOutputLimitExceededException(_outputName, _maxBytes));
                    }
                }

                return Encoding.UTF8.GetString(
                    captured.GetBuffer(),
                    0,
                    checked((int)captured.Length));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(readBuffer);
            }
        }
    }
}
