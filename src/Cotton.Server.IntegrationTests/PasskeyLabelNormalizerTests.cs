// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services.Passkeys;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class PasskeyLabelNormalizerTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Normalize_RegistrationWithoutLabelReturnsNull(string? label)
        {
            string? normalized = PasskeyLabelNormalizer.Normalize(label);

            Assert.That(normalized, Is.Null);
        }

        [Test]
        public void Normalize_RegistrationWithCustomLabelTrimsWhitespace()
        {
            string? normalized = PasskeyLabelNormalizer.Normalize("  Office security key  ");

            Assert.That(normalized, Is.EqualTo("Office security key"));
        }

        [Test]
        public void Normalize_RegistrationLabelIsBoundedAfterTrimming()
        {
            string label = $"  {new string('x', PasskeyLabelNormalizer.MaximumLength + 20)}  ";

            string? normalized = PasskeyLabelNormalizer.Normalize(label);

            Assert.That(normalized, Has.Length.EqualTo(PasskeyLabelNormalizer.MaximumLength));
        }
    }
}
