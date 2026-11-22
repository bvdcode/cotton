// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Numerics;
using System.Text;
using Mediana.Drawing;
using Mediana.Drawing.Fonts;

namespace Cotton.Previews
{
    internal class PreviewTextRenderer
    {
        private static readonly FontRequest Font = new(string.Empty);
        private readonly TextRenderer _renderer = new(new() { SubstituteMissingFonts = true });
        private readonly TrueTypeFont _font;
        private readonly float _size;
        private readonly float _scale;
        private readonly Dictionary<(TrueTypeFont Font, int Glyph), (float X, float Y, float Width, float Height)> _bounds = [];

        public PreviewTextRenderer(float size)
        {
            _font = _renderer.ResolveFont(Font)
                ?? throw new InvalidOperationException("The default preview font is unavailable.");
            _size = size;
            _scale = size / _font.Units;
        }

        public float MeasureAdvance(int codepoint)
        {
            (TrueTypeFont font, int glyph) = _renderer.ResolveGlyph(Font, codepoint);
            return (font.Advance(glyph) * _size) / font.Units;
        }

        public float MeasureWidth(string text)
        {
            float x = 0;
            float left = float.PositiveInfinity;
            float right = float.NegativeInfinity;
            int previous = 0;
            TrueTypeFont? previousFont = null;
            foreach (Rune rune in text.EnumerateRunes())
            {
                (TrueTypeFont font, int glyph) = _renderer.ResolveGlyph(Font, rune.Value);
                float scale = _size / font.Units;
                if (previous != 0 && previousFont == font)
                {
                    x += font.Kerning(previous, glyph) * scale;
                }
                if (!_bounds.TryGetValue((font, glyph), out (float X, float Y, float Width, float Height) bounds))
                {
                    bounds = _renderer.Outline(font, glyph).GetBounds();
                    _bounds.Add((font, glyph), bounds);
                }
                if (bounds.Width > 0)
                {
                    left = Math.Min(left, x + (bounds.X * scale));
                    right = Math.Max(right, x + ((bounds.X + bounds.Width) * scale));
                }
                x += font.Advance(glyph) * scale;
                previous = glyph;
                previousFont = font;
            }
            return float.IsFinite(left) ? right - left : 0;
        }

        public void Draw(RasterCanvas canvas, string text, float originX, float originY, Rgba32 color, float outputScale = 1)
        {
            float x = originX;
            float baseline = originY + (_font.Ascent * _scale);
            int previous = 0;
            TrueTypeFont? previousFont = null;
            foreach (Rune rune in text.EnumerateRunes())
            {
                if (rune.Value == '\n')
                {
                    x = originX;
                    baseline += (_font.Ascent - _font.Descent) * _scale;
                    previous = 0;
                    previousFont = null;
                    continue;
                }
                (TrueTypeFont font, int glyph) = _renderer.ResolveGlyph(Font, rune.Value);
                float scale = _size / font.Units;
                if (previous != 0 && previousFont == font)
                {
                    x += font.Kerning(previous, glyph) * scale;
                }
                Matrix3x2 transform = Matrix3x2.CreateScale(scale, -scale)
                    * Matrix3x2.CreateTranslation(x, baseline)
                    * Matrix3x2.CreateScale(outputScale);
                _renderer.DrawGlyphAntialiased(canvas, font, glyph, color, transform);
                x += font.Advance(glyph) * scale;
                previous = glyph;
                previousFont = font;
            }
        }
    }
}
