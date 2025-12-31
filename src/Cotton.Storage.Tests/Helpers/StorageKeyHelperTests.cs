// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Storage.Helpers;

namespace Cotton.Storage.Tests.Helpers
{
    [TestFixture]
    public class StorageKeyHelperTests
    {
        [Test]
        public void NormalizeUid_TrimAndLowerCase_CorrectlyNormalizes()
        {
            // Arrange
            string input = "  AbCDef  ";
            string expected = "abcdef";

            // Act
            string result = StorageKeyHelper.NormalizeUid(input);

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void NormalizeUid_TooShort_ThrowsException()
        {
            // Arrange
            string input = "abc";

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => StorageKeyHelper.NormalizeUid(input));
            Assert.That(ex.Message, Does.Contain("too short"));
            Assert.That(ex.Message, Does.Contain("6"));
        }

        [Test]
        public void NormalizeUid_InvalidCharacters_ThrowsException()
        {
            // Arrange
            var invalidInputs = new[]
            {
                "abc def",
                "abc/def",
                "abc\\def",
                "abc:def",
                "abc..def",
                "абвгде",
                "abc@def"
            };

            // Act & Assert
            foreach (var input in invalidInputs)
            {
                var ex = Assert.Throws<ArgumentException>(() => StorageKeyHelper.NormalizeUid(input),
                    $"Should throw for input: {input}");
                Assert.That(ex.Message, Does.Contain("invalid character"),
                    $"Exception message should mention invalid character for: {input}");
            }
        }

        [Test]
        public void NormalizeUid_Idempotency_ReturnsConsistentResult()
        {
            // Arrange
            string input = "  ABCDEF123  ";

            // Act
            string normalized1 = StorageKeyHelper.NormalizeUid(input);
            string normalized2 = StorageKeyHelper.NormalizeUid(normalized1);

            // Assert
            Assert.That(normalized2, Is.EqualTo(normalized1));
        }

        [Test]
        public void NormalizeUid_ValidHexCharacters_Success()
        {
            // Arrange
            var validInputs = new[]
            {
                "abcdef",
                "123456",
                "abc123",
                "a0b1c2d3e4f5"
            };

            // Act & Assert
            foreach (var input in validInputs)
            {
                Assert.DoesNotThrow(() => StorageKeyHelper.NormalizeUid(input),
                    $"Should not throw for valid input: {input}");
            }
        }

        [Test]
        public void GetSegments_CorrectlySplits()
        {
            // Arrange
            string uid = "abcdef123456";

            // Act
            var (p1, p2, fileName) = StorageKeyHelper.GetSegments(uid);

            Assert.Multiple(() =>
            {
                // Assert
                Assert.That(p1, Is.EqualTo("ab"));
                Assert.That(p2, Is.EqualTo("cd"));
                Assert.That(fileName, Is.EqualTo("ef123456"));
            });
        }

        [Test]
        public void GetSegments_MinimumLength_CorrectlySplits()
        {
            // Arrange
            string uid = "abcdef";

            // Act
            var (p1, p2, fileName) = StorageKeyHelper.GetSegments(uid);

            Assert.Multiple(() =>
            {
                // Assert
                Assert.That(p1, Is.EqualTo("ab"));
                Assert.That(p2, Is.EqualTo("cd"));
                Assert.That(fileName, Is.EqualTo("ef"));
            });
        }

        [Test]
        public void GetSegments_VeryLongUid_CorrectlySplits()
        {
            // Arrange
            string uid = "ab" + "cd" + new string('e', 100);

            // Act
            var (p1, p2, fileName) = StorageKeyHelper.GetSegments(uid);

            Assert.Multiple(() =>
            {
                // Assert
                Assert.That(p1, Is.EqualTo("ab"));
                Assert.That(p2, Is.EqualTo("cd"));
                Assert.That(fileName, Has.Length.EqualTo(100));
            });
        }

        [Test]
        public void GetSegments_InvalidUid_ThrowsBeforeSegmentation()
        {
            // Arrange
            string uid = "ab/cd";

            // Act & Assert
            Assert.Throws<ArgumentException>(() => StorageKeyHelper.GetSegments(uid));
        }
    }
}
