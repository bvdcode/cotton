// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using NUnit.Framework;

namespace Cotton.Validators.Tests;

public sealed class RelativePathValidatorTests
{
    [Test]
    public void TryNormalizeAndValidate_NormalizesSeparatorsAndSegments()
    {
        bool isValid = RelativePathValidator.TryNormalizeAndValidate(
            @"Docs\Reports\report..",
            out string normalized,
            out string error);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.True);
            Assert.That(normalized, Is.EqualTo("Docs/Reports/report"));
            Assert.That(error, Is.Empty);
        });
    }

    [TestCase("../outside.txt")]
    [TestCase("docs/../outside.txt")]
    [TestCase("docs/./file.txt")]
    [TestCase("docs//file.txt")]
    [TestCase("docs/ /file.txt")]
    [TestCase("CON.txt")]
    [TestCase("docs/a:b.txt")]
    public void TryNormalizeAndValidate_RejectsUnsafeOrUnsupportedSegments(string input)
    {
        bool isValid = RelativePathValidator.TryNormalizeAndValidate(input, out string normalized, out string error);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.False);
            Assert.That(normalized, Is.Empty);
            Assert.That(error, Is.Not.Empty);
        });
    }

    [Test]
    public void NormalizeOrThrow_ThrowsForTraversalPath()
    {
        Assert.Throws<ArgumentException>(() => RelativePathValidator.NormalizeOrThrow(@"folder\..\outside.txt"));
    }
}
