// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.Fonts.Resources;
using Lokad.OoxPdf.Fonts;

namespace Cotton.Previews
{
    internal class OfficePreviewFontResolver : IFontResolver
    {
        private static readonly MemoryFontProgramSource Arial = new("Arial", StaticFonts.GetFontBytes(StaticFontName.Arial));

        public FontFaceResolution Resolve(FontRequest request)
        {
            bool substituted = !request.FamilyName.Equals("Arial", StringComparison.OrdinalIgnoreCase)
                || request.Bold || request.Italic;
            return new FontFaceResolution(request.FamilyName, "Arial", new FontStyleKey(), Arial, substituted);
        }
    }
}
