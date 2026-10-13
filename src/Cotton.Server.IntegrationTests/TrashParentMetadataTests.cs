// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;

namespace Cotton.Server.IntegrationTests
{
    public class TrashParentMetadataTests
    {
        [Test]
        public void ReplaceParent_ChangesOnlyMatchingAncestorAndRetainsItemMetadata()
        {
            TrashParent root = new(Guid.NewGuid(), "Default", null);
            TrashParent folder = new(Guid.NewGuid(), "Documents", new() { ["policy"] = "true" });
            TrashParent nested = new(Guid.NewGuid(), "Nested", new() { ["other"] = "kept" });
            Dictionary<string, string> item = new() { ["en"] = "file-ciphertext", ["custom"] = "retained" };
            Dictionary<string, string> stored = TrashParentMetadata.Write(item, [root, folder, nested]);
            TrashParent replacement = folder with { Name = "opaque", Metadata = new() { ["en"] = "name-ciphertext", ["policy"] = "true" } };

            Dictionary<string, string> result = TrashParentMetadata.ReplaceParent(stored, [root, folder, nested], replacement);
            List<TrashParent> restored = TrashParentMetadata.Read(result)!;
            Assert.Multiple(() =>
            {
                Assert.That(restored.Select(x => x.Id), Is.EqualTo(new[] { root.Id, folder.Id, nested.Id }));
                Assert.That(restored[1].Name, Is.EqualTo("opaque"));
                Assert.That(restored[1].Metadata, Is.EquivalentTo(replacement.Metadata!));
                Assert.That(restored[2].Metadata, Is.EquivalentTo(nested.Metadata!));
                Assert.That(result["originalParentPath"], Is.EqualTo("opaque/Nested"));
                Assert.That(result["en"], Is.EqualTo("file-ciphertext"));
                Assert.That(result["custom"], Is.EqualTo("retained"));
                Assert.That(stored["originalParentPath"], Is.EqualTo("Documents/Nested"));
            });
        }

        [Test]
        public void ReplaceParent_RejectsMissingOrAmbiguousReferences()
        {
            TrashParent folder = new(Guid.NewGuid(), "Documents", null);
            Assert.Throws<ArgumentException>(() => TrashParentMetadata.ReplaceParent(null, [], folder));
            Assert.Throws<ArgumentException>(() => TrashParentMetadata.ReplaceParent(null, [folder, folder], folder));
        }

        [Test]
        public void CaptureMetadata_DoesNotNestOldTrashSnapshots()
        {
            Dictionary<string, string> source = new()
            {
                [TrashParentMetadata.Key] = "previous snapshot",
                ["originalParentPath"] = "previous path",
                ["en"] = "encrypted-name",
            };
            Assert.That(TrashParentMetadata.CopyFolderMetadata(source),
                Is.EquivalentTo(new Dictionary<string, string> { ["en"] = "encrypted-name" }));
            Assert.That(source.Count, Is.EqualTo(3));
        }
    }
}
