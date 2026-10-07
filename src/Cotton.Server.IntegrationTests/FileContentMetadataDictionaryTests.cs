// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.IntegrationTests
{
    public class FileContentMetadataDictionaryTests
    {
        [Test]
        public void ReplaceManagedValues_PreservesEmptyTagsAndCustomValues()
        {
            Dictionary<string, string> previous = new()
            {
                ["contentMetadata.extractionProcessed"] = "true",
                ["media.removed"] = "old",
                ["custom.label"] = "Keep",
            };
            Dictionary<string, string> extracted = new()
            {
                ["media.format.tags.empty"] = string.Empty,
                ["media.format.tags.spaces"] = "  ",
            };

            Dictionary<string, string> result = FileContentMetadataDictionary.ReplaceManagedValues(previous, extracted);

            Assert.Multiple(() =>
            {
                Assert.That(result[FileContentMetadataKeys.ExtractionVersion], Is.EqualTo("1"));
                Assert.That(result["media.format.tags.empty"], Is.Empty);
                Assert.That(result["media.format.tags.spaces"], Is.EqualTo("  "));
                Assert.That(result["custom.label"], Is.EqualTo("Keep"));
                Assert.That(result, Does.Not.ContainKey("media.removed"));
                Assert.That(result, Does.Not.ContainKey("contentMetadata.extractionProcessed"));
                Assert.That(previous["media.removed"], Is.EqualTo("old"));
            });
        }

        [Test]
        public void FailedAttempt_StoresVersionAndError_AndSuccessfulExtractionClearsError()
        {
            Dictionary<string, string> previous = new()
            {
                ["image.width"] = "old",
                ["custom.label"] = "Keep",
            };
            Dictionary<string, string> failed = FileContentMetadataDictionary.MarkProcessed(previous, "Invalid metadata.");
            Dictionary<string, string> retried = FileContentMetadataDictionary.ReplaceManagedValues(failed,
                new Dictionary<string, string> { ["image.width"] = "32" });

            Assert.Multiple(() =>
            {
                Assert.That(FileContentMetadataDictionary.HasCurrentVersion(failed), Is.True);
                Assert.That(failed[FileContentMetadataKeys.ExtractionError], Is.EqualTo("Invalid metadata."));
                Assert.That(failed, Does.Not.ContainKey("image.width"));
                Assert.That(failed["custom.label"], Is.EqualTo("Keep"));
                Assert.That(FileContentMetadataDictionary.IsProjectionKey(FileContentMetadataKeys.ExtractionError), Is.False);
                Assert.That(retried, Does.Not.ContainKey(FileContentMetadataKeys.ExtractionError));
                Assert.That(retried["image.width"], Is.EqualTo("32"));
                Assert.That(retried["custom.label"], Is.EqualTo("Keep"));
            });
        }

        [Test]
        public void HasCurrentVersion_DoesNotAcceptLegacyFlagsOrEmptyDictionary()
        {
            Assert.Multiple(() =>
            {
                Assert.That(FileContentMetadataDictionary.HasCurrentVersion(null), Is.False);
                Assert.That(FileContentMetadataDictionary.HasCurrentVersion([]), Is.False);
                Assert.That(FileContentMetadataDictionary.HasCurrentVersion(new()
                {
                    ["contentMetadata.extractionProcessed"] = "true",
                    ["image.width"] = "32",
                }), Is.False);
            });
        }
    }
}
