using PhotoSauce.MagicScaler;
using PhotoSauce.NativeCodecs.Libheif;
using PhotoSauce.NativeCodecs.Libwebp;

namespace Cotton.Previews
{
    internal static class PreviewCodecBootstrap
    {
        private static int _initialized;

        public static void EnsureInitialized()
        {
            if (Interlocked.Exchange(ref _initialized, 1) == 1)
            {
                return;
            }

            CodecManager.Configure(codecs =>
            {
                codecs.UseLibheif();
                codecs.UseLibwebp();
            });
        }
    }
}
