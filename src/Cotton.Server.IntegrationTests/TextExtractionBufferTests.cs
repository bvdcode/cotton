// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.TextExtraction;
using NUnit.Framework;
using System.Text;

namespace Cotton.Server.IntegrationTests
{
    public class TextExtractionBufferTests
    {
        [TestCase(1, "a", true)]
        [TestCase(2, "a", true)]
        [TestCase(3, "aБ", true)]
        [TestCase(6, "aБ", true)]
        [TestCase(7, "aБ😀", true)]
        [TestCase(10, "aБ😀中", true)]
        [TestCase(11, "aБ😀中z", false)]
        [TestCase(12, "aБ😀中z", false)]
        public void Append_EnforcesUtf8LimitAcrossEveryCharacterBoundary(int limit, string expected, bool truncated)
        {
            const string Input = "aБ😀中z";
            for (int split = 0; split <= Input.Length; split++)
            {
                TextExtractionBuffer buffer = new(limit);
                buffer.Append(Input.AsSpan(0, split));
                buffer.Append(Input.AsSpan(split));
                TextExtractionResult result = buffer.GetResult();
                Assert.That(result, Is.EqualTo(new TextExtractionResult(expected, truncated)), $"Split at {split}");
                Assert.That(Encoding.UTF8.GetByteCount(result.Text), Is.LessThanOrEqualTo(limit));
            }
        }

        [Test]
        public void Normalize_PreservesInlineWordsAndLineBreaksWithoutCountingTrailingWhitespace()
        {
            string expected = $"Hello world{Environment.NewLine}Б😀";
            TextExtractionBuffer buffer = new(Encoding.UTF8.GetByteCount(expected));
            foreach (string part in new[] { "  Hel", "lo ", " world\r", "\nБ", "\ud83d", "\ude00 ", "\t\n" })
            {
                buffer.AppendNormalized(part);
            }
            Assert.That(buffer.GetResult(), Is.EqualTo(new TextExtractionResult(expected, false)));
            buffer.AppendNormalized("next");
            Assert.That(buffer.GetResult(), Is.EqualTo(new TextExtractionResult(expected, true)));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void NonPositiveLimit_IsRejected(int limit)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TextExtractionBuffer(limit));
        }
    }
}
