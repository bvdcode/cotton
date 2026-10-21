// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Extensions;

namespace Cotton.Server.IntegrationTests
{
    public class TokenBulkDeletionTests : BulkDeletionTestBase
    {
        [TestCase("files")]
        [TestCase("user-file")]
        [TestCase("user-nodes")]
        [TestCase("user-downloads")]
        [TestCase("nodes")]
        [TestCase("user-shares")]
        public async Task TokenDeletion_PreservesOwnershipScopeSignaturesAndTransaction(string operation)
        {
            Layout firstLayout = AddLayout(Users[0]);
            Layout secondLayout = AddLayout(Users[1]);
            Node[] nodes = [AddNode(Users[0], firstLayout, "first"), AddNode(Users[0], firstLayout, "second"),
                AddNode(Users[1], secondLayout, "third")];
            NodeFile[] files = nodes.Select(node => AddFile(node.Owner, node, AddManifest(), "test.txt")).ToArray();
            DownloadToken[,] downloads = new DownloadToken[3, 3];
            NodeShareToken[,] shares = new NodeShareToken[3, 3];
            for (int user = 0; user < 3; user++)
            {
                for (int file = 0; file < 3; file++)
                {
                    downloads[user, file] = Context.DownloadTokens.Add(new DownloadToken
                    {
                        CreatedByUser = Users[user], NodeFile = files[file], FileName = files[file].Name,
                        Token = Guid.NewGuid().ToString("N")
                    }).Entity;
                    shares[user, file] = Context.NodeShareTokens.Add(new NodeShareToken
                    {
                        CreatedByUser = Users[user], Node = nodes[file], Name = nodes[file].Name,
                        Token = Guid.NewGuid().ToString("N")
                    }).Entity;
                }
            }
            CancellationToken ct = CancellationToken.None;
            switch (operation)
            {
                case "files":
                    await AssertDeletionAsync([downloads[0, 0], downloads[1, 0], downloads[2, 0]],
                        () => Context.DownloadTokens.DeleteDownloadTokensForFilesAsync([files[0].Id, files[0].Id, Guid.NewGuid()], ct));
                    break;
                case "user-file":
                    await AssertDeletionAsync([downloads[0, 0]],
                        () => Context.DownloadTokens.DeleteUserDownloadTokensForFileAsync(Users[0].Id, files[0].Id, ct));
                    break;
                case "user-nodes":
                    await AssertDeletionAsync([downloads[0, 0], downloads[0, 2]],
                        () => Context.DownloadTokens.DeleteUserDownloadTokensForNodesAsync(Users[0].Id, [nodes[0].Id, nodes[2].Id], ct));
                    break;
                case "user-downloads":
                    await AssertDeletionAsync([downloads[0, 0], downloads[0, 1], downloads[0, 2],
                        downloads[1, 0], downloads[1, 1], downloads[2, 0], downloads[2, 1]],
                        () => Context.DownloadTokens.DeleteDownloadTokensForUserAsync(Users[0].Id, ct));
                    break;
                case "nodes":
                    await AssertDeletionAsync([shares[0, 0], shares[1, 0], shares[2, 0]],
                        () => Context.NodeShareTokens.DeleteNodeShareTokensForNodesAsync([nodes[0].Id, nodes[0].Id, Guid.NewGuid()], ct));
                    break;
                case "user-shares":
                    await AssertDeletionAsync([shares[0, 0], shares[0, 1], shares[0, 2],
                        shares[1, 0], shares[1, 1], shares[2, 0], shares[2, 1]],
                        () => Context.NodeShareTokens.DeleteNodeShareTokensForUserAsync(Users[0].Id, ct));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation));
            }
        }

        [Test]
        public async Task EmptyTokenSelections_PreserveAllRows()
        {
            Node node = AddNode(Users[0], AddLayout(Users[0]), "root");
            NodeFile file = AddFile(Users[0], node, AddManifest(), "test.txt");
            Context.DownloadTokens.Add(new DownloadToken
            {
                CreatedByUser = Users[0], NodeFile = file, FileName = file.Name, Token = "download"
            });
            Context.NodeShareTokens.Add(new NodeShareToken { CreatedByUser = Users[0], Node = node, Name = node.Name, Token = "share" });
            await AssertDeletionAsync([], async () =>
                await Context.DownloadTokens.DeleteDownloadTokensForFilesAsync([], CancellationToken.None)
                + await Context.DownloadTokens.DeleteUserDownloadTokensForNodesAsync(Users[0].Id, [], CancellationToken.None)
                + await Context.NodeShareTokens.DeleteNodeShareTokensForNodesAsync([], CancellationToken.None));
        }
    }
}
