// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.Globalization;
using System.Text;

namespace Cotton.Validators
{
    public static class NameValidator
    {
        private static readonly System.Buffers.SearchValues<char> _forbiddenAscii = System.Buffers.SearchValues.Create("/\\<>:\"|?*\0");

        // Policy
        public const int MaxSegmentBytes = 255;     // segment (name) in UTF-8
        public const int MaxGraphemes = 255;     // to prevent abuse with combining characters

        // Frequently abused zero-width/format characters
        private static readonly int[] ForbiddenCodepoints =
        [
            0x200B, // ZERO WIDTH SPACE
            0x200C, // ZERO WIDTH NON-JOINER
            0x200D, // ZERO WIDTH JOINER
            0x2060, // WORD JOINER
            0xFEFF, // ZERO WIDTH NO-BREAK SPACE (BOM)
        ];

        private static readonly string[] ReservedBaseNamesCI =
        [
            "CON","PRN","AUX","NUL","CLOCK$",
            "COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
            "LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9",
        ];

        /// <summary>
        /// Normalizing and validating helper: normalizes the name (NFC, trims) and validates it.
        /// Returns the normalized name.
        /// </summary>
        public static bool TryNormalizeAndValidate(
            string input,
            out string normalized,
            out string errorMessage)
        {
            normalized = string.Empty;

            if (string.IsNullOrWhiteSpace(input))
            {
                errorMessage = "Name cannot be empty or whitespace.";
                return false;
            }

            // 1) Unicode NFC
            string name = input.Normalize(NormalizationForm.FormC);

            // 2) Trims: remove leading/trailing spaces; remove trailing dots
            name = name.Trim();
            while (name.EndsWith('.'))
            {
                name = name[..^1];
            }

            if (name.Length == 0)
            {
                errorMessage = "Name becomes empty after trimming trailing spaces/dots.";
                return false;
            }

            // 3) Forbid "." and ".."
            if (name == "." || name == "..")
            {
                errorMessage = "Name cannot be '.' or '..'.";
                return false;
            }

            // 4) Control characters (C0/C1) and explicit ASCII forbids
            if (name.Any(char.IsControl) || name.AsSpan().IndexOfAny(_forbiddenAscii) >= 0)
            {
                errorMessage = "Name contains forbidden control or path characters.";
                return false;
            }

            // 5) Forbid zero-width/format characters from the list
            if (ContainsForbiddenZeroWidth(name))
            {
                errorMessage = "Name contains zero-width/format characters.";
                return false;
            }

            // 6) Forbid ending with space/dot (after normalization this should not occur,
            //    but check in case of non-standard whitespace)
            if (EndsWithSpaceOrDot(name))
            {
                errorMessage = "Name cannot end with a space or dot.";
                return false;
            }

            // 7) Length in UTF-8 bytes
            if (Encoding.UTF8.GetByteCount(name) > MaxSegmentBytes)
            {
                errorMessage = $"Name exceeds {MaxSegmentBytes} bytes in UTF-8.";
                return false;
            }

            // 8) Grapheme (user-perceived characters) limit
            if (CountGraphemes(name) > MaxGraphemes)
            {
                errorMessage = $"Name exceeds {MaxGraphemes} user-perceived characters.";
                return false;
            }

            // 9) Windows reserved: check base name without extension and trailing dots/spaces
            var baseName = GetBaseNameForWindows(name);
            if (IsReservedBaseName(baseName))
            {
                errorMessage = $"Name base part '{baseName}' is reserved on Windows.";
                return false;
            }

            normalized = name;
            errorMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// Original contract: just validates without returning normalized name.
        /// Internally calls TryNormalizeAndValidate and ignores normalized.
        /// </summary>
        public static bool IsValidName(string name, out string errorMessage)
            => TryNormalizeAndValidate(name, out _, out errorMessage);

        // ---------------- helpers ----------------

        private static bool EndsWithSpaceOrDot(string s)
        {
            if (s.Length == 0)
            {
                return false;
            }
            var last = s[^1];
            return last == '.' || char.IsWhiteSpace(last);
        }

        private static bool ContainsForbiddenZeroWidth(string s)
        {
            // Quick check using explicit list + general ban for U+200B..U+200D and U+2060, U+FEFF
            foreach (var ch in s.EnumerateRunes())
            {
                int cp = ch.Value;
                // explicit
                if (Array.IndexOf(ForbiddenCodepoints, cp) >= 0)
                {
                    return true;
                }
                // generalized format? (not banning all Cf to avoid overreach)
                // if (CharUnicodeInfo.GetUnicodeCategory((char)cp) == UnicodeCategory.Format) return true;
            }
            return false;
        }

        private static int CountGraphemes(string s)
        {
            // StringInfo counts text elements (grapheme clusters)
            var e = StringInfo.GetTextElementEnumerator(s);
            int count = 0;
            while (e.MoveNext())
            {
                count++;
            }
            return count;
        }

        private static string GetBaseNameForWindows(string name)
        {
            // Windows reserves the base name up to the first '.', without trailing spaces/dots
            var trimmed = name.TrimEnd(' ', '.');
            int dot = trimmed.IndexOf('.');
            var basePart = dot >= 0 ? trimmed[..dot] : trimmed;
            return basePart;
        }

        private static bool IsReservedBaseName(string baseName)
        {
            if (baseName.Length == 0)
            {
                return false;
            }
            var up = baseName.ToUpperInvariant();
            return ReservedBaseNamesCI.Contains(up);
        }

        public static string NormalizeAndGetNameKey(string normalized)
        {
            bool isValid = TryNormalizeAndValidate(normalized, out string norm, out string error);
            if (!isValid)
            {
                throw new ArgumentException($"Invalid name for key generation: {error}");
            }
            return GetNameKey(norm);
        }

        public static string GetNameKey(string normalized)
        {
            var sb = new StringBuilder();
            var enumr = StringInfo.GetTextElementEnumerator(normalized);
            while (enumr.MoveNext())
            {
                var element = enumr.GetTextElement();
                var folded = element.Normalize(NormalizationForm.FormD);
                var sbFolded = new StringBuilder();
                foreach (var ch in folded.EnumerateRunes())
                {
                    var category = CharUnicodeInfo.GetUnicodeCategory(ch.Value);
                    if (category == UnicodeCategory.NonSpacingMark ||
                        category == UnicodeCategory.SpacingCombiningMark ||
                        category == UnicodeCategory.EnclosingMark)
                    {
                        continue;
                    }
                    sbFolded.Append(ch.ToString().ToLowerInvariant());
                }
                var final = sbFolded.ToString().Normalize(NormalizationForm.FormC);
                sb.Append(final);
            }
            return sb.ToString();
        }
    }
}
