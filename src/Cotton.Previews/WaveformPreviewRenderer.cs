// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Numerics;
using Mediana.Drawing;

namespace Cotton.Previews
{
    internal static class WaveformPreviewRenderer
    {
        public static void Draw(PixelBuffer pixels, ReadOnlySpan<float> amplitudes)
        {
            int size = pixels.Width;
            int bars = amplitudes.Length;
            float gap = Math.Max(5f, size / 48f);
            float padding = Math.Max(10f, gap * 1.5f);
            float width = Math.Max(1f, (size - 2f * padding - (bars - 1) * gap) / bars);
            float left = (size - (bars * width + (bars - 1) * gap)) / 2f;
            float centerY = size / 2f;
            float minHeight = Math.Max(4f, size * 0.06f);
            float maxHeight = size * 0.82f;
            Rgba32 color = new(PreviewColorPalette.AccentGreenRed,
                PreviewColorPalette.AccentGreenGreen, PreviewColorPalette.AccentGreenBlue, 255);
            RasterCanvas canvas = new(pixels);
            for (int i = 0; i < bars; i++)
            {
                float height = Math.Max(minHeight, amplitudes[i] * maxHeight);
                float x = left + i * (width + gap);
                float y = centerY - height / 2f;
                float radius = width / 2f;
                if (height <= width)
                {
                    canvas.FillPathAntialiased(Circle(x + radius, centerY, radius), color, Matrix3x2.Identity);
                    continue;
                }
                canvas.FillPathAntialiased(new VectorPath().Rectangle(x, y + radius, width, height - width), color, Matrix3x2.Identity);
                canvas.FillPathAntialiased(Circle(x + radius, y + radius, radius), color, Matrix3x2.Identity);
                canvas.FillPathAntialiased(Circle(x + radius, y + height - radius, radius), color, Matrix3x2.Identity);
            }
        }

        private static VectorPath Circle(float x, float y, float radius)
        {
            float control = radius * 0.55228475f;
            return new VectorPath().MoveTo(x + radius, y)
                .CubicTo(x + radius, y + control, x + control, y + radius, x, y + radius)
                .CubicTo(x - control, y + radius, x - radius, y + control, x - radius, y)
                .CubicTo(x - radius, y - control, x - control, y - radius, x, y - radius)
                .CubicTo(x + control, y - radius, x + radius, y - control, x + radius, y).Close();
        }
    }
}
