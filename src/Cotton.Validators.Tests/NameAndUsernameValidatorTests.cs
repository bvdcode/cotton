// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Validators;
using NUnit.Framework;

namespace Cotton.Validators.Tests;

public class NameAndUsernameValidatorTests
{
    [Test]
    public void Username_TryNormalizeAndValidate_TrimsAndLowercases()
    {
        bool isValid = UsernameValidator.TryNormalizeAndValidate(
            "  UsEr.Name-1  ",
            out string normalized,
            out string error);

        Assert.That(isValid, Is.True);
        Assert.That(normalized, Is.EqualTo("user.name-1"));
        Assert.That(error, Is.Empty);
    }

    [Test]
    public void Username_TryNormalizeAndValidate_RejectsTooShortValue()
    {
        bool isValid = UsernameValidator.TryNormalizeAndValidate("a", out _, out string error);

        Assert.That(isValid, Is.False);
        Assert.That(error, Does.Contain("between"));
    }

    [Test]
    public void Username_TryNormalizeAndValidate_RejectsNonLetterStart()
    {
        bool isValid = UsernameValidator.TryNormalizeAndValidate("1admin", out _, out string error);

        Assert.That(isValid, Is.False);
        Assert.That(error, Does.Contain("must start with a letter"));
    }

    [Test]
    public void Username_TryNormalizeAndValidate_RejectsConsecutiveSeparators()
    {
        bool isValid = UsernameValidator.TryNormalizeAndValidate("john__doe", out _, out string error);

        Assert.That(isValid, Is.False);
        Assert.That(error, Does.Contain("non-consecutive separators"));
    }

    [Test]
    public void Username_TryNormalizeAndValidate_AcceptsMaxLength()
    {
        string username = "a" + new string('b', UsernameValidator.MaxLength - 1);

        bool isValid = UsernameValidator.TryNormalizeAndValidate(username, out string normalized, out string error);

        Assert.That(isValid, Is.True);
        Assert.That(normalized.Length, Is.EqualTo(UsernameValidator.MaxLength));
        Assert.That(error, Is.Empty);
    }

    [Test]
    public void Name_TryNormalizeAndValidate_TrimsAndRemovesTrailingDots()
    {
        bool isValid = NameValidator.TryNormalizeAndValidate("  report..  ", out string normalized, out string error);

        Assert.That(isValid, Is.True);
        Assert.That(normalized, Is.EqualTo("report"));
        Assert.That(error, Is.Empty);
    }

    [Test]
    public void Name_TryNormalizeAndValidate_RejectsDotAndDotDot()
    {
        bool isDotValid = NameValidator.TryNormalizeAndValidate(".", out _, out _);
        bool isDotDotValid = NameValidator.TryNormalizeAndValidate("..", out _, out _);

        Assert.That(isDotValid, Is.False);
        Assert.That(isDotDotValid, Is.False);
    }

    [Test]
    public void Name_TryNormalizeAndValidate_RejectsForbiddenPathCharacters()
    {
        bool isValid = NameValidator.TryNormalizeAndValidate("a/b", out _, out string error);

        Assert.That(isValid, Is.False);
        Assert.That(error, Does.Contain("forbidden"));
    }

    [Test]
    public void Name_TryNormalizeAndValidate_RejectsZeroWidthCharacters()
    {
        string input = "file\u200Bname";

        bool isValid = NameValidator.TryNormalizeAndValidate(input, out _, out string error);

        Assert.That(isValid, Is.False);
        Assert.That(error, Does.Contain("zero-width"));
    }

    [Test]
    public void Name_TryNormalizeAndValidate_RejectsWindowsReservedBaseName()
    {
        bool isValid = NameValidator.TryNormalizeAndValidate("CON.txt", out _, out string error);

        Assert.That(isValid, Is.False);
        Assert.That(error, Does.Contain("reserved"));
    }

    [Test]
    public void Name_NormalizeAndGetNameKey_FoldsDiacriticsAndCase()
    {
        string key = NameValidator.NormalizeAndGetNameKey("École");

        Assert.That(key, Is.EqualTo("ecole"));
    }

    [Test]
    public void Name_NormalizeAndGetNameKey_ThrowsForInvalidName()
    {
        Assert.Throws<ArgumentException>(() => NameValidator.NormalizeAndGetNameKey(".."));
    }
}
