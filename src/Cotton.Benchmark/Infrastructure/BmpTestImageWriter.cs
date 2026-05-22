// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Buffers;
using System.Text;

namespace Cotton.Benchmark.Infrastructure
{
    internal readonly record struct BmpImageSpec(int Width, int Height)
    {
        public long PixelCount => (long)Width * Height;

        public double Megapixels => PixelCount / 1_000_000d;

        public long RowStrideBytes => ((Width * 3L) + 3) & ~3L;

        public long PixelBytes => RowStrideBytes * Height;

        public long FileSizeBytes => 54 + PixelBytes;

        public long DecodedRgbaBytes => PixelCount * 4;

        public string Dimensions => $"{Width}x{Height}";
    }

    internal static class BmpTestImageWriter
    {
        public static async Task WriteAsync(string path, BmpImageSpec spec, CancellationToken cancellationToken)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(spec.Width);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(spec.Height);

            if (spec.FileSizeBytes > uint.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(spec), "BMP test image must fit the classic 32-bit BMP file header.");
            }

            await using var stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 128 * 1024,
                FileOptions.SequentialScan | FileOptions.Asynchronous);

            WriteHeader(stream, spec);
            await WritePixelRowsAsync(stream, spec, cancellationToken).ConfigureAwait(false);
        }

        private static void WriteHeader(Stream stream, BmpImageSpec spec)
        {
            using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
            writer.Write((byte)'B');
            writer.Write((byte)'M');
            writer.Write((uint)spec.FileSizeBytes);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write(54u);
            writer.Write(40u);
            writer.Write(spec.Width);
            writer.Write(spec.Height);
            writer.Write((ushort)1);
            writer.Write((ushort)24);
            writer.Write(0u);
            writer.Write((uint)spec.PixelBytes);
            writer.Write(2835);
            writer.Write(2835);
            writer.Write(0u);
            writer.Write(0u);
        }

        private static async Task WritePixelRowsAsync(Stream stream, BmpImageSpec spec, CancellationToken cancellationToken)
        {
            if (spec.RowStrideBytes > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(spec), "BMP row stride is too large for a managed row buffer.");
            }

            int stride = (int)spec.RowStrideBytes;
            byte[] row = ArrayPool<byte>.Shared.Rent(stride);
            try
            {
                for (int y = 0; y < spec.Height; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    FillRow(row.AsSpan(0, stride), spec.Width, y);
                    await stream.WriteAsync(row.AsMemory(0, stride), cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(row);
            }
        }

        private static void FillRow(Span<byte> row, int width, int y)
        {
            row.Clear();
            for (int x = 0; x < width; x++)
            {
                int offset = x * 3;
                row[offset] = (byte)((x + y) & 0xff);
                row[offset + 1] = (byte)((x * 3) & 0xff);
                row[offset + 2] = (byte)((y * 5) & 0xff);
            }
        }
    }
}
