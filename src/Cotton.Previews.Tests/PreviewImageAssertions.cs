// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Cotton.Previews.Tests
{
    internal static class PreviewImageAssertions
    {
        public static int CountVisiblePixels(Image<Rgba32> image)
        {
            int count = 0;
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    if (image[x, y].A > 128)
                    {
                        count++;
                    }
                }
            }
            return count;
        }
    }
}
