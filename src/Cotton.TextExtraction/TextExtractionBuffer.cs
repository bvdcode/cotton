// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Text;

namespace Cotton.TextExtraction
{
    public class TextExtractionBuffer
    {
        private readonly StringBuilder _text = new();
        private readonly int _maxUtf8Bytes;
        private int _utf8Bytes;
        private char? _pendingHighSurrogate;
        private string _separator = string.Empty;

        public TextExtractionBuffer(int maxUtf8Bytes)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxUtf8Bytes);
            _maxUtf8Bytes = maxUtf8Bytes;
        }

        public bool IsTruncated { get; private set; }

        internal void MarkTruncated() => IsTruncated = true;

        public void Append(ReadOnlySpan<char> value)
        {
            if (IsTruncated || value.IsEmpty)
            {
                return;
            }
            if (_pendingHighSurrogate is char high)
            {
                _pendingHighSurrogate = null;
                if (char.IsLowSurrogate(value[0]))
                {
                    AppendComplete([high, value[0]]);
                    value = value[1..];
                }
                else
                {
                    AppendComplete(Rune.ReplacementChar.ToString());
                }
            }
            if (!value.IsEmpty && char.IsHighSurrogate(value[^1]))
            {
                _pendingHighSurrogate = value[^1];
                value = value[..^1];
            }
            AppendComplete(value);
        }

        public void AppendNormalized(ReadOnlySpan<char> value)
        {
            while (!value.IsEmpty && !IsTruncated)
            {
                if (char.IsWhiteSpace(value[0]))
                {
                    if (value[0] is '\r' or '\n')
                    {
                        AppendLineBreak();
                    }
                    else if (_separator.Length == 0 && HasText)
                    {
                        _separator = " ";
                    }
                    value = value[1..];
                    continue;
                }
                int length = 1;
                while (length < value.Length && !char.IsWhiteSpace(value[length]))
                {
                    length++;
                }
                AppendSeparated(value[..length]);
                value = value[length..];
            }
        }

        public void AppendSeparated(ReadOnlySpan<char> value)
        {
            if (value.IsEmpty)
            {
                return;
            }
            Append(_separator);
            _separator = string.Empty;
            Append(value);
        }

        public void AppendLineBreak()
        {
            if (HasText)
            {
                _separator = Environment.NewLine;
            }
        }

        public void AppendLines(IEnumerable<string?> lines, CancellationToken cancellationToken)
        {
            foreach (string? line in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AppendNormalized(line);
                AppendLineBreak();
                if (IsTruncated)
                {
                    return;
                }
            }
        }

        public TextExtractionResult GetResult()
        {
            if (_pendingHighSurrogate is not null)
            {
                _pendingHighSurrogate = null;
                AppendComplete(Rune.ReplacementChar.ToString());
            }
            return new(_text.ToString().Trim(), IsTruncated);
        }

        private bool HasText => _text.Length > 0 || _pendingHighSurrogate is not null;

        private void AppendComplete(ReadOnlySpan<char> value)
        {
            if (IsTruncated || value.IsEmpty)
            {
                return;
            }
            int byteCount = Encoding.UTF8.GetByteCount(value);
            if (byteCount <= _maxUtf8Bytes - _utf8Bytes)
            {
                _text.Append(value);
                _utf8Bytes += byteCount;
                return;
            }
            Span<char> characters = stackalloc char[2];
            foreach (Rune rune in value.EnumerateRunes())
            {
                if (rune.Utf8SequenceLength > _maxUtf8Bytes - _utf8Bytes)
                {
                    IsTruncated = true;
                    return;
                }
                int length = rune.EncodeToUtf16(characters);
                _text.Append(characters[..length]);
                _utf8Bytes += rune.Utf8SequenceLength;
            }
        }
    }
}
