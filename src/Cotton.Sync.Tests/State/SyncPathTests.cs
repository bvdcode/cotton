// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.State;

namespace Cotton.Sync.Tests.State
{
    public class SyncPathTests
    {
        [Test]
        public void Normalize_NormalizesSeparators()
        {
            string normalized = SyncPath.Normalize(@"Docs\Reports\file.txt");

            Assert.That(normalized, Is.EqualTo("Docs/Reports/file.txt"));
        }

        [TestCase("/etc/passwd")]
        [TestCase(@"\Windows\System32")]
        [TestCase(@"\\server\share\file.txt")]
        public void Normalize_RejectsRootedPaths(string relativePath)
        {
            SyncPathValidationException? exception = Assert.Throws<SyncPathValidationException>(() => SyncPath.Normalize(relativePath));

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.Not.Null);
                Assert.That(exception!.RelativePath, Is.EqualTo(relativePath));
                Assert.That(exception.Reason, Does.Contain("rooted"));
            });
        }

        [Test]
        public void Normalize_UsesUnicodeFormC()
        {
            string decomposed = "Docs/Cafe\u0301.txt";

            string normalized = SyncPath.Normalize(decomposed);

            Assert.Multiple(() =>
            {
                Assert.That(normalized, Is.EqualTo("Docs/Caf\u00e9.txt"));
                Assert.That(SyncPath.ToKey(decomposed), Is.EqualTo(SyncPath.ToKey("Docs/Caf\u00e9.txt")));
            });
        }

        [TestCase("CON")]
        [TestCase("Docs/NUL.txt")]
        [TestCase("COM1.log")]
        [TestCase("LPT9")]
        public void Normalize_RejectsWindowsReservedDeviceNames(string relativePath)
        {
            SyncPathValidationException? exception = Assert.Throws<SyncPathValidationException>(() => SyncPath.Normalize(relativePath));

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.Not.Null);
                Assert.That(exception!.RelativePath, Is.EqualTo(relativePath));
                Assert.That(exception.Reason, Does.Contain("device name"));
            });
        }

        [TestCase("Docs/file:name.txt")]
        [TestCase("Docs/question?.txt")]
        [TestCase("Docs/star*.txt")]
        public void Normalize_RejectsWindowsReservedCharacters(string relativePath)
        {
            SyncPathValidationException? exception = Assert.Throws<SyncPathValidationException>(() => SyncPath.Normalize(relativePath));

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.Not.Null);
                Assert.That(exception!.RelativePath, Is.EqualTo(relativePath));
                Assert.That(exception.Reason, Does.Contain("reserved characters"));
            });
        }

        [TestCase("Docs/name.")]
        [TestCase("Docs/name ")]
        public void Normalize_RejectsWindowsTrailingDotOrSpace(string relativePath)
        {
            SyncPathValidationException? exception = Assert.Throws<SyncPathValidationException>(() => SyncPath.Normalize(relativePath));

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.Not.Null);
                Assert.That(exception!.RelativePath, Is.EqualTo(relativePath));
                Assert.That(exception.Reason, Does.Contain("space or dot"));
            });
        }

        [Test]
        public void Normalize_RejectsTooLongPathSegments()
        {
            string segment = new('a', 256);
            string relativePath = "Docs/" + segment;

            SyncPathValidationException? exception = Assert.Throws<SyncPathValidationException>(() => SyncPath.Normalize(relativePath));

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.Not.Null);
                Assert.That(exception!.Segment, Is.EqualTo(segment));
                Assert.That(exception.Reason, Does.Contain("segment length"));
            });
        }

        [Test]
        public void Normalize_RejectsTooLongRelativePaths()
        {
            string relativePath = string.Join('/', Enumerable.Repeat("folder", 5462));

            SyncPathValidationException? exception = Assert.Throws<SyncPathValidationException>(() => SyncPath.Normalize(relativePath));

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.Not.Null);
                Assert.That(exception!.Segment, Is.Null);
                Assert.That(exception.Reason, Does.Contain("relative path length"));
            });
        }
    }
}
