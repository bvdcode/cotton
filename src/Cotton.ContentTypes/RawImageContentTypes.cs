// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.ContentTypes
{
    public static class RawImageContentTypes
    {
        public const string Cr2 = "image/x-canon-cr2";
        public const string Cr3 = "image/x-canon-cr3";
        public const string Nef = "image/x-nikon-nef";
        public const string Nrw = "image/x-nikon-nrw";
        public const string Arw = "image/x-sony-arw";
        public const string Dng = "image/x-adobe-dng";
        public const string Raf = "image/x-fujifilm-raf";
        public const string Orf = "image/x-olympus-orf";
        public const string Rw2 = "image/x-panasonic-rw2";
        public const string Pef = "image/x-pentax-pef";
        public const string Srw = "image/x-samsung-srw";

        public static IReadOnlyList<string> All { get; } =
        [
            Cr2, Cr3, Nef, Nrw, Arw, Dng, Raf, Orf, Rw2, Pef, Srw,
        ];
    }
}
