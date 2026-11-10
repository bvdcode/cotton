// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Crypto.Tests
{
    public class StreamOwnershipTests
    {
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public async Task DecryptAsync_InvalidHeader_RespectsStreamOwnership(bool leaveInputOpen, bool leaveOutputOpen)
        {
            using AesGcmStreamCipher cipher = new(new byte[AesGcmStreamCipher.KeySize]);
            using MemoryStream input = new([1, 2, 3]);
            using MemoryStream output = new();

            await Assert.ThatAsync(
                () => cipher.DecryptAsync(input, output, leaveInputOpen, leaveOutputOpen),
                Throws.InstanceOf<EndOfStreamException>());

            Assert.That(input.CanRead, Is.EqualTo(leaveInputOpen));
            Assert.That(output.CanWrite, Is.EqualTo(leaveOutputOpen));
        }
    }
}
