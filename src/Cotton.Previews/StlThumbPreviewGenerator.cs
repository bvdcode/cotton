using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.InteropServices;

namespace Cotton.Previews
{
    public class StlThumbPreviewGenerator : IPreviewGenerator
    {
        private readonly string _modelExtension;
        private readonly string[] _supportedContentTypes;

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

        public int Version => 0;

        public IEnumerable<string> SupportedContentTypes => _supportedContentTypes;

        public async Task<byte[]> GeneratePreviewWebPAsync(Stream stream, int size)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

            int bufferSize = checked(size * size * 4);
            byte[] rgbaBuffer = new byte[bufferSize];
            string modelFilePath = Path.Combine(Path.GetTempPath(), $"cotton-model-{Guid.NewGuid():N}{_modelExtension}");

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

                bool rendered;
                try
                {
                    rendered = StlThumbNative.RenderToBuffer(rgbaBuffer, (uint)size, (uint)size, modelFilePath);
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
                try
                {
                    File.Delete(modelFilePath);
                }
                catch
                {
                    // Temporary-file cleanup failures must not hide the original render error.
                }
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
