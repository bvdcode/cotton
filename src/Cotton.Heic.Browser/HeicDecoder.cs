// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Heic.Net;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace Cotton.Heic.Browser
{
    [SupportedOSPlatform("browser")]
    public partial class HeicDecoder
    {
        [JSExport]
        public static JSObject Decode(byte[] encoded)
        {
            HeicDocument document = HeicDocument.Load(encoded);
            RgbaImage image = document.DecodePrimary();
            return CreateImage(image.Width, image.Height, image.Pixels.ToArray());
        }

        [JSImport("createImage", "heic")]
        private static partial JSObject CreateImage(int width, int height, byte[] pixels);
    }
}
