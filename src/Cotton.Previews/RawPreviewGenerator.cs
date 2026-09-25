// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.ContentTypes;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Buffers;
using System.Buffers.Binary;

namespace Cotton.Previews
{
    public class RawPreviewGenerator : IPreviewGenerator
    {
        private const long MaxSourceBytes = 512L * 1024 * 1024;
        private const long MaxDecodedPixels = 150_000_000;
        private const int MaxJpegCandidates = 64;
        private const int MaxJpegHeaderBytes = 512 * 1024;
        private const int ScanBufferBytes = 81920;

        public string Id => "raw";

        public int Version => 1;

        public int Priority => 0;

        public IEnumerable<string> SupportedContentTypes => RawImageContentTypes.All;

        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

            string temporaryFile = Path.Combine(Path.GetTempPath(), $"cotton_raw_{Guid.NewGuid():N}.bin");
            try
            {
                if (stream.CanSeek)
                {
                    stream.Position = 0;
                }
                await WriteSourceAsync(stream, temporaryFile);

                List<(long Offset, int Width, int Height)> candidates = await FindJpegsAsync(temporaryFile);
                if (candidates.Count == 0)
                {
                    throw new InvalidDataException("RAW file has no supported embedded JPEG image.");
                }

                (long offset, int _, int _) = SelectImage(candidates, size);
                await using FileStream jpegStream = new(temporaryFile, FileMode.Open, FileAccess.Read, FileShare.Read,
                    ScanBufferBytes, FileOptions.Asynchronous | FileOptions.RandomAccess);
                jpegStream.Position = offset;
                using Image<Rgba32> image = await Image.LoadAsync<Rgba32>(jpegStream);

                ushort? orientation = ReadOrientation(temporaryFile);
                if (orientation.HasValue)
                {
                    image.Metadata.ExifProfile ??= new ExifProfile();
                    image.Metadata.ExifProfile.SetValue(ExifTag.Orientation, orientation.Value);
                }
                image.Mutate(context => context.AutoOrient());
                return await ImagePreviewGenerator.EncodeMaxResizedWebpAsync(image, size);
            }
            finally
            {
                PreviewTemporaryFile.TryDelete(temporaryFile);
            }
        }

        private static ushort? ReadOrientation(string path)
        {
            IReadOnlyList<MetadataExtractor.Directory> directories = ImageMetadataReader.ReadMetadata(path);
            ExifIfd0Directory? exif = directories.OfType<ExifIfd0Directory>()
                .FirstOrDefault(directory => directory.ContainsTag(ExifDirectoryBase.TagOrientation));
            if (exif is null)
            {
                return null;
            }

            int orientation = exif.GetInt32(ExifDirectoryBase.TagOrientation);
            return orientation is >= 1 and <= 8 ? (ushort)orientation : null;
        }

        private static (long Offset, int Width, int Height) SelectImage(
            List<(long Offset, int Width, int Height)> candidates, int size)
        {
            if (size <= PreviewGeneratorProvider.DefaultSmallPreviewSize)
            {
                var suitable = candidates.Where(candidate => Math.Max(candidate.Width, candidate.Height) >= size);
                if (suitable.Any())
                {
                    return suitable.MinBy(candidate => (long)candidate.Width * candidate.Height);
                }
            }

            return candidates.MaxBy(candidate => (long)candidate.Width * candidate.Height);
        }

        private static async Task<List<(long Offset, int Width, int Height)>> FindJpegsAsync(string path)
        {
            List<long> offsets = [];
            await using (FileStream source = new(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                ScanBufferBytes, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(ScanBufferBytes);
                byte previousThree = 0;
                byte previousTwo = 0;
                byte previousOne = 0;
                long position = 0;
                try
                {
                    int read;
                    while ((read = await source.ReadAsync(buffer)) > 0)
                    {
                        for (int index = 0; index < read; index++, position++)
                        {
                            byte current = buffer[index];
                            // Embedded photos start with DQT or APP; RAW payloads can also contain SOI markers.
                            if (previousThree == 0xff && previousTwo == 0xd8 && previousOne == 0xff
                                && (current == 0xdb || current is >= 0xe0 and <= 0xef))
                            {
                                offsets.Add(position - 3);
                                if (offsets.Count > MaxJpegCandidates)
                                {
                                    throw new InvalidDataException("RAW file has too many embedded image candidates.");
                                }
                            }
                            previousThree = previousTwo;
                            previousTwo = previousOne;
                            previousOne = current;
                        }
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }

            List<(long Offset, int Width, int Height)> candidates = [];
            await using FileStream jpegSource = new(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                ScanBufferBytes, FileOptions.Asynchronous | FileOptions.RandomAccess);
            foreach (long offset in offsets)
            {
                (int Width, int Height)? dimensions = await ReadJpegDimensionsAsync(jpegSource, offset);
                if (dimensions.HasValue)
                {
                    candidates.Add((offset, dimensions.Value.Width, dimensions.Value.Height));
                }
            }
            return candidates;
        }

        private static async Task<(int Width, int Height)?> ReadJpegDimensionsAsync(FileStream source, long offset)
        {
            int readLength = checked((int)Math.Min(MaxJpegHeaderBytes, source.Length - offset));
            byte[] header = ArrayPool<byte>.Shared.Rent(readLength);
            try
            {
                source.Position = offset;
                await source.ReadExactlyAsync(header.AsMemory(0, readLength));
                return ParseJpegDimensions(header.AsSpan(0, readLength));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(header);
            }
        }

        private static (int Width, int Height)? ParseJpegDimensions(ReadOnlySpan<byte> header)
        {
            if (header.Length < 4 || header[0] != 0xff || header[1] != 0xd8)
            {
                return null;
            }

            int position = 2;
            while (position + 4 <= header.Length)
            {
                if (header[position++] != 0xff)
                {
                    return null;
                }
                while (position < header.Length && header[position] == 0xff)
                {
                    position++;
                }
                if (position >= header.Length)
                {
                    return null;
                }

                byte marker = header[position++];
                if (marker is 0xd9 or 0xda)
                {
                    return null;
                }
                if (marker is 0x01 or >= 0xd0 and <= 0xd7)
                {
                    continue;
                }
                if (position + 2 > header.Length)
                {
                    return null;
                }

                int segmentLength = BinaryPrimitives.ReadUInt16BigEndian(header.Slice(position, 2));
                position += 2;
                int payloadLength = segmentLength - 2;
                if (payloadLength < 0 || position + payloadLength > header.Length)
                {
                    return null;
                }
                if (marker is >= 0xc0 and <= 0xc3 or >= 0xc5 and <= 0xc7
                    or >= 0xc9 and <= 0xcb or >= 0xcd and <= 0xcf)
                {
                    if (payloadLength < 5)
                    {
                        return null;
                    }
                    int height = BinaryPrimitives.ReadUInt16BigEndian(header.Slice(position + 1, 2));
                    int width = BinaryPrimitives.ReadUInt16BigEndian(header.Slice(position + 3, 2));
                    return width > 0 && height > 0 && (long)width * height <= MaxDecodedPixels
                        ? (width, height) : null;
                }
                position += payloadLength;
            }
            return null;
        }

        private static async Task WriteSourceAsync(Stream source, string path)
        {
            await using FileStream destination = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(ScanBufferBytes);
            long written = 0;
            try
            {
                while (true)
                {
                    int read = await source.ReadAsync(buffer);
                    if (read == 0)
                    {
                        return;
                    }
                    if (written + read > MaxSourceBytes)
                    {
                        throw new InvalidDataException("RAW file exceeds the preview source limit.");
                    }
                    await destination.WriteAsync(buffer.AsMemory(0, read));
                    written += read;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
