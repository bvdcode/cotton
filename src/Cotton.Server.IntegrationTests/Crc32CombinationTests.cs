// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.IntegrationTests
{
    public class Crc32CombinationTests
    {
        [Test]
        public void Combine_MatchesWholeContentAtEverySplit()
        {
            byte[] bytes = new byte[1024];
            new Random(42).NextBytes(bytes);
            Crc32Accumulator complete = new();
            complete.Append(bytes);
            for (int split = 0; split <= bytes.Length; split++)
            {
                Crc32Accumulator prefix = new();
                prefix.Append(bytes.AsSpan(0, split));
                Crc32Accumulator suffix = new();
                suffix.Append(bytes.AsSpan(split));
                Assert.That(Crc32Accumulator.Combine(prefix.Value, suffix.Value, bytes.Length - split),
                    Is.EqualTo(complete.Value), $"Split {split}");
            }
        }

        [TestCase(1L, 0xc0f227bcu)]
        [TestCase(4_294_967_295L, 0xd9c06f5eu)]
        [TestCase(4_294_967_296L, 0xc0f227bcu)]
        [TestCase(125_627_793_408L, 0xb3447eb4u)]
        public void Combine_MatchesZlibForLongSuffixes(long length, uint expected)
        {
            Assert.That(Crc32Accumulator.Combine(0xcbf43926u, 0x12345678u, length), Is.EqualTo(expected));
        }
    }
}
