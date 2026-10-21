// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Extensions;
using EasyExtensions.EntityFrameworkCore.Database;

namespace Cotton.Server.IntegrationTests
{
    public class AccountBulkDeletionTests : BulkDeletionTestBase
    {
        [Test]
        public async Task UserDeletion_PreservesOtherUsersAndTheirSignatures()
        {
            await AssertDeletionAsync([Users[0]], () => Context.Users.DeleteUserAsync(Users[0].Id, CancellationToken.None));
        }

        [Test]
        public async Task NotificationDeletion_PreservesOtherUsersNotifications()
        {
            Notification[] rows = Users.Select(user => new Notification { User = user, Title = user.Username }).ToArray();
            Context.Notifications.AddRange(rows);
            await AssertDeletionAsync([rows[0]], () => Context.Notifications.DeleteNotificationsForUserAsync(Users[0].Id, CancellationToken.None));
        }

        [Test]
        public async Task PasskeyDeletion_PreservesOtherUsersCredentials()
        {
            UserPasskeyCredential[] rows = Users.Select(user => new UserPasskeyCredential
            {
                User = user, CredentialId = user.Id.ToByteArray(), PublicKey = [1, 2, 3], UserHandle = user.Id.ToByteArray()
            }).ToArray();
            Context.UserPasskeyCredentials.AddRange(rows);
            await AssertDeletionAsync([rows[0]], () => Context.UserPasskeyCredentials.DeletePasskeysForUserAsync(Users[0].Id, CancellationToken.None));
        }

        [Test]
        public async Task RefreshTokenDeletion_PreservesOtherUsersSessions()
        {
            ExtendedRefreshToken[] rows = Users.Select(user => new ExtendedRefreshToken
            {
                UserId = user.Id, Token = user.Username, SessionId = user.Username, AuthType = AuthType.Credentials,
                IpAddress = IPAddress.Loopback, UserAgent = "test", Device = "test", City = "test", Region = "test", Country = "test"
            }).ToArray();
            Context.RefreshTokens.AddRange(rows);
            await AssertDeletionAsync([rows[0]], () => Context.RefreshTokens.DeleteRefreshTokensForUserAsync(Users[0].Id, CancellationToken.None));
        }

        [Test]
        public async Task SyncChangeDeletion_PreservesOtherOwnersHistory()
        {
            SyncChange[] rows = Users.Select(user => new SyncChange
            {
                Owner = user, Name = "test", Kind = SyncChangeKind.FileCreated,
                ItemId = Guid.NewGuid(), LayoutId = Guid.NewGuid(), ParentNodeId = Guid.NewGuid()
            }).ToArray();
            Context.SyncChanges.AddRange(rows);
            await AssertDeletionAsync([rows[0]], () => Context.SyncChanges.DeleteSyncChangesForUserAsync(Users[0].Id, CancellationToken.None));
        }

        [Test]
        public async Task ChunkOwnershipDeletion_PreservesSharedChunkAndOtherOwners()
        {
            Chunk chunk = AddChunk();
            ChunkOwnership[] rows = Users.Select(user => new ChunkOwnership { Owner = user, Chunk = chunk }).ToArray();
            Context.ChunkOwnerships.AddRange(rows);
            await AssertDeletionAsync([rows[0]], () => Context.ChunkOwnerships.DeleteChunkOwnershipsForUserAsync(Users[0].Id, CancellationToken.None));
        }

        [Test]
        public async Task LayoutDeletion_PreservesOtherOwnersLayouts()
        {
            Layout[] layouts = [AddLayout(Users[0]), AddLayout(Users[0]), AddLayout(Users[1])];
            await AssertDeletionAsync([layouts[0], layouts[1]], () => Context.UserLayouts.DeleteLayoutsForUserAsync(Users[0].Id, CancellationToken.None));
        }

        [Test]
        public async Task LeafDeletion_PreservesParentsAndOtherOwnersLeaves()
        {
            Layout firstLayout = AddLayout(Users[0]);
            Node root = AddNode(Users[0], firstLayout, "root");
            Node leaf = AddNode(Users[0], firstLayout, "leaf");
            leaf.SetParent(root);
            AddNode(Users[1], AddLayout(Users[1]), "other");
            await AssertDeletionAsync([leaf], () => Context.Nodes.DeleteLeafNodesForUserAsync(Users[0].Id, CancellationToken.None), repeat: false);
        }

        [Test]
        public async Task FileDeletion_PreservesSharedManifestAndOtherOwnersFiles()
        {
            FileManifest manifest = AddManifest();
            NodeFile[] files = Users.Select(user => AddFile(user, AddNode(user, AddLayout(user), "root"), manifest, "test.txt")).ToArray();
            await AssertDeletionAsync([files[0]], () => Context.NodeFiles.DeleteNodeFilesForUserAsync(Users[0].Id, CancellationToken.None));
        }
    }
}
