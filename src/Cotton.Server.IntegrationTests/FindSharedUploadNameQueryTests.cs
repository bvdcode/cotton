// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Layouts;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Globalization;

namespace Cotton.Server.IntegrationTests
{
    [NonParallelizable]
    public class FindSharedUploadNameQueryTests : IntegrationTestBase
    {
        private Node _root = null!;
        private FileManifest _manifest = null!;

        [SetUp]
        public async Task PrepareFolder()
        {
            await RecreateDatabaseAsync();
            User owner = new() { Username = "uploadowner", PasswordPhc = "test", WebDavTokenPhc = "test" };
            Layout layout = new() { Owner = owner, IsActive = true };
            _root = new Node { Owner = owner, Layout = layout, Type = NodeType.Default };
            _root.SetName("uploads");
            _manifest = new FileManifest { ProposedContentHash = SHA256.HashData([]) };
            DbContext.Nodes.Add(_root);
            DbContext.FileManifests.Add(_manifest);
            DbContext.Entry(_manifest).Property<string>("ContentType").CurrentValue = string.Empty;
            await DbContext.SaveChangesAsync();
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(1000)]
        public async Task FindsFreeSuffixWithOneQueryRegardlessOfConflictCount(int conflicts)
        {
            for (int index = 0; index < conflicts; index++)
            {
                string name = index == 0 ? "document.txt" : string.Create(CultureInfo.InvariantCulture, $"document ({index}).txt");
                if (index % 2 == 0)
                {
                    AddFile(_root, name);
                }
                else
                {
                    AddFolder(_root, name);
                }
            }
            await DbContext.SaveChangesAsync();

            string expected = conflicts == 0 ? "document.txt" : string.Create(CultureInfo.InvariantCulture, $"document ({conflicts}).txt");
            Assert.That(await FindAsync(_root.Id, _root.OwnerId, "document.txt"), Is.EqualTo(expected));
        }

        [Test]
        public async Task ReusesFirstGapAndNormalizesFileAndFolderNames()
        {
            AddFile(_root, "RÉSUMÉ.TXT");
            AddFolder(_root, "résumé (1).txt");
            AddFile(_root, "resume (3).txt");
            await DbContext.SaveChangesAsync();

            Assert.That(await FindAsync(_root.Id, _root.OwnerId, "resume.txt"), Is.EqualTo("resume (2).txt"));
        }

        [Test]
        public async Task IgnoresNamesInOtherFoldersAndForOtherOwners()
        {
            Node child = AddFolder(_root, "child");
            AddFile(child, "document.txt");
            AddFolder(child, "document (1).txt");
            AddFile(_root, "occupied.txt");
            await DbContext.SaveChangesAsync();

            Assert.That(await FindAsync(_root.Id, _root.OwnerId, "document.txt"), Is.EqualTo("document.txt"));
            Assert.That(await FindAsync(_root.Id, Guid.NewGuid(), "occupied.txt"), Is.EqualTo("occupied.txt"));
        }

        private async Task<string> FindAsync(Guid nodeId, Guid ownerId, string name)
        {
            List<string> commands = [];
            DbContextOptionsBuilder<CottonDbContext> options = new();
            options.UseNpgsql(TestDataSource)
                .LogTo(commands.Add, [RelationalEventId.CommandExecuted]);
            await using CottonDbContext database = new(options.Options);
            FindSharedUploadNameQueryHandler handler = new(database);
            string result = await handler.Handle(new FindSharedUploadNameQuery(nodeId, ownerId, name), CancellationToken.None);
            Assert.That(commands, Has.Count.EqualTo(1), "Selecting a free name must use one database command.");
            Assert.That(database.ChangeTracker.Entries(), Is.Empty);
            return result;
        }

        private void AddFile(Node parent, string name)
        {
            NodeFile file = new() { Owner = parent.Owner, Node = parent, FileManifest = _manifest };
            file.SetName(name);
            DbContext.NodeFiles.Add(file);
        }

        private Node AddFolder(Node parent, string name)
        {
            Node folder = new() { Owner = parent.Owner, Layout = parent.Layout, Parent = parent, Type = NodeType.Default };
            folder.SetName(name);
            DbContext.Nodes.Add(folder);
            return folder;
        }
    }
}
