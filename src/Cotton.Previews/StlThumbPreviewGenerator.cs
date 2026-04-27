using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace Cotton.Previews
{
    public class StlThumbPreviewGenerator : IPreviewGenerator
    {
        private readonly string _modelExtension;
        private readonly string[] _supportedContentTypes;
        private const string ThreeMfExtension = ".3mf";

        public StlThumbPreviewGenerator()
            : this(".stl", ["model/stl", "application/sla"])
        {
        }

        private StlThumbPreviewGenerator(string modelExtension, string[] supportedContentTypes)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(modelExtension);
            _modelExtension = modelExtension.StartsWith(".", StringComparison.Ordinal)
                ? modelExtension
                : "." + modelExtension;
            _supportedContentTypes = supportedContentTypes;
        }

        public static StlThumbPreviewGenerator CreateObjGenerator()
        {
            return new StlThumbPreviewGenerator(".obj", ["model/obj"]);
        }

        public static StlThumbPreviewGenerator CreateThreeMfGenerator()
        {
            return new StlThumbPreviewGenerator(
                ".3mf",
                ["model/3mf", "application/vnd.ms-package.3dmanufacturing-3dmodel+xml"]);
        }

        public int Version => 1;

        public IEnumerable<string> SupportedContentTypes => _supportedContentTypes;

        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

            int bufferSize = checked(size * size * 4);
            byte[] rgbaBuffer = new byte[bufferSize];
            string modelFilePath = Path.Combine(Path.GetTempPath(), $"cotton-model-{Guid.NewGuid():N}{_modelExtension}");
            string? normalizedThreeMfPath = null;

            try
            {
                if (stream.CanSeek)
                {
                    stream.Position = 0;
                }

                await using (FileStream fileStream = new(
                    modelFilePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    options: FileOptions.Asynchronous))
                {
                    await stream.CopyToAsync(fileStream).ConfigureAwait(false);
                }

                bool rendered = RenderToBuffer(rgbaBuffer, size, modelFilePath);

                if (!rendered && string.Equals(_modelExtension, ThreeMfExtension, StringComparison.OrdinalIgnoreCase))
                {
                    normalizedThreeMfPath = await TryNormalizeThreeMfArchiveAsync(modelFilePath).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(normalizedThreeMfPath))
                    {
                        rendered = RenderToBuffer(rgbaBuffer, size, normalizedThreeMfPath);
                    }
                }

                if (!rendered)
                {
                    throw new InvalidOperationException("stl-thumb failed to render model preview.");
                }

                using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(rgbaBuffer, size, size);
                using var outputStream = new MemoryStream();
                await image.SaveAsWebpAsync(outputStream).ConfigureAwait(false);
                return outputStream.ToArray();
            }
            finally
            {
                TryDeleteFile(modelFilePath);

                if (!string.IsNullOrWhiteSpace(normalizedThreeMfPath))
                {
                    TryDeleteFile(normalizedThreeMfPath);
                }
            }
        }

        private static bool RenderToBuffer(byte[] rgbaBuffer, int size, string modelFilePath)
        {
            try
            {
                return StlThumbNative.RenderToBuffer(rgbaBuffer, (uint)size, (uint)size, modelFilePath);
            }
            catch (DllNotFoundException ex)
            {
                throw new InvalidOperationException("stl-thumb native library was not found.", ex);
            }
            catch (EntryPointNotFoundException ex)
            {
                throw new InvalidOperationException("stl-thumb entry point render_to_buffer was not found.", ex);
            }
            catch (BadImageFormatException ex)
            {
                throw new InvalidOperationException("stl-thumb native library architecture is incompatible.", ex);
            }
        }

        private static async Task<string?> TryNormalizeThreeMfArchiveAsync(string sourcePath)
        {
            string normalizedPath = Path.Combine(Path.GetTempPath(), $"cotton-model-normalized-{Guid.NewGuid():N}{ThreeMfExtension}");

            try
            {
                await using FileStream inputFileStream = new(
                    sourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 81920,
                    options: FileOptions.Asynchronous);

                using ZipArchive sourceArchive = new(inputFileStream, ZipArchiveMode.Read, leaveOpen: false);

                await using FileStream outputFileStream = new(
                    normalizedPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    options: FileOptions.Asynchronous);

                using ZipArchive normalizedArchive = new(outputFileStream, ZipArchiveMode.Create, leaveOpen: false);
                foreach (ZipArchiveEntry sourceEntry in sourceArchive.Entries)
                {
                    ZipArchiveEntry normalizedEntry = normalizedArchive.CreateEntry(sourceEntry.FullName, CompressionLevel.Optimal);
                    normalizedEntry.LastWriteTime = sourceEntry.LastWriteTime;

                    await using Stream sourceEntryStream = sourceEntry.Open();
                    await using Stream normalizedEntryStream = normalizedEntry.Open();
                    await sourceEntryStream.CopyToAsync(normalizedEntryStream).ConfigureAwait(false);
                }

                return normalizedPath;
            }
            catch (InvalidDataException)
            {
                TryDeleteFile(normalizedPath);
                return null;
            }
            catch (NotSupportedException)
            {
                TryDeleteFile(normalizedPath);
                return null;
            }
            catch (IOException)
            {
                TryDeleteFile(normalizedPath);
                return null;
            }

        }

        private static void TryDeleteFile(string filePath)
        {
            try
            {
                File.Delete(filePath);
            }
            catch
            {
                // Temporary-file cleanup failures must not hide the original render error.
            }
        }

        private static class StlThumbNative
        {
#pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
            [DllImport("stl_thumb", EntryPoint = "render_to_buffer", CallingConvention = CallingConvention.Cdecl)]
            [return: MarshalAs(UnmanagedType.I1)]
            internal static extern bool RenderToBuffer(
                byte[] buffer,
                uint width,
                uint height,
                [MarshalAs(UnmanagedType.LPUTF8Str)] string modelFilename);
#pragma warning restore SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
        }
    }
}
