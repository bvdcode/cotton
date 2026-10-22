// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Extensions;

namespace Cotton.Server.IntegrationTests
{
    public class ContentBulkDeletionTests : BulkDeletionTestBase
    {
        [TestCase(false)]
        [TestCase(true)]
        public async Task ChunkDeletion_PreservesUnselectedChunksAndSignatures(bool empty)
        {
            Chunk[] chunks = [AddChunk(), AddChunk(), AddChunk()];
            object[] removed = empty ? [] : [chunks[0], chunks[1]];
            byte[][] selection = empty ? [] : [chunks[0].Hash.ToArray(), chunks[1].Hash.ToArray(), chunks[0].Hash.ToArray(), SHA256.HashData("missing"u8)];
            await AssertDeletionAsync(removed, () => Context.Chunks.DeleteChunksAsync(selection, CancellationToken.None));
        }

        [TestCase("manifest")]
        [TestCase("ids")]
        [TestCase("empty")]
        public async Task EmbeddingDeletion_PreservesUnselectedFragmentsAndManifests(string operation)
        {
            FileManifest[] manifests = [AddManifest(), AddManifest()];
            FileEmbedding[,] embeddings = new FileEmbedding[2, 3];
            for (int manifest = 0; manifest < 2; manifest++)
            {
                for (int fragment = 0; fragment < 3; fragment++)
                {
                    embeddings[manifest, fragment] = Context.FileEmbeddings.Add(new FileEmbedding
                    {
                        FileManifest = manifests[manifest], FragmentIndex = fragment, IndexVersion = fragment + 1, Embedding = [0.1f, 0.2f]
                    }).Entity;
                }
            }
            switch (operation)
            {
                case "manifest":
                    await AssertDeletionAsync([embeddings[0, 0], embeddings[0, 1], embeddings[0, 2]],
                        () => Context.FileEmbeddings.DeleteFileEmbeddingsAsync(manifests[0].Id, CancellationToken.None));
                    break;
                case "ids":
                    await AssertDeletionAsync([embeddings[0, 0], embeddings[1, 1]],
                        () => Context.FileEmbeddings.DeleteFileEmbeddingsByIdAsync(
                            [embeddings[0, 0].Id, embeddings[1, 1].Id, embeddings[0, 0].Id, Guid.NewGuid()], CancellationToken.None));
                    break;
                case "empty":
                    await AssertDeletionAsync([], () => Context.FileEmbeddings.DeleteFileEmbeddingsByIdAsync([], CancellationToken.None));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task ManifestDeletion_PreservesReferencedAndUnselectedManifests(bool empty)
        {
            FileManifest[] manifests = [AddManifest(), AddManifest(), AddManifest()];
            AddFile(Users[0], AddNode(Users[0], AddLayout(Users[0]), "root"), manifests[1], "shared.txt");
            object[] removed = empty ? [] : [manifests[0]];
            Guid[] selection = empty ? [] : [manifests[0].Id, manifests[1].Id, manifests[0].Id, Guid.NewGuid()];
            await AssertDeletionAsync(removed,
                () => Context.FileManifests.DeleteOrphanedFileManifestsAsync(selection, CancellationToken.None));
        }
    }
}
