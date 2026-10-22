// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Cotton.Server.IntegrationTests.Abstractions
{
    [NonParallelizable]
    public abstract class BulkDeletionTestBase : IntegrationTestBase
    {
        private readonly DatabaseIntegrityDescriptorRegistry _descriptors = new([
            new UserIntegrityDescriptor(), new ChunkIntegrityDescriptor(), new FileManifestIntegrityDescriptor(),
            new NodeIntegrityDescriptor(), new NodeFileIntegrityDescriptor(), new DownloadTokenIntegrityDescriptor(),
            new NodeShareTokenIntegrityDescriptor(), new UserPasskeyCredentialIntegrityDescriptor(),
            new UserExternalIdentityIntegrityDescriptor(), new OidcLoginStateIntegrityDescriptor(),
            new OidcProviderIntegrityDescriptor(), new ExtendedRefreshTokenIntegrityDescriptor()]);
        private DatabaseFieldProtector _fieldProtector = null!;
        private DatabaseIntegrityKeyProvider _keys = null!;
        private DatabaseIntegrityProtector _protector = null!;
        private IDbContextTransaction _transaction = null!;
        protected CottonDbContext Context { get; private set; } = null!;
        protected User[] Users { get; private set; } = [];

        [OneTimeSetUp]
        public async Task CreateDatabase()
        {
            await RecreateDatabaseAsync();
        }

        [SetUp]
        public async Task BeginTransaction()
        {
            CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
                "testtesttesttesttesttesttesttest");
            _keys = new(settings);
            _fieldProtector = new(settings, NullLogger<DatabaseFieldProtector>.Instance);
            _protector = new(_keys);
            DatabaseIntegrityChangeSigner signer = new(_protector, _descriptors, NullDatabaseIntegrityFailureReporter.Instance);
            DbContextOptionsBuilder<CottonDbContext> options = new();
            options.UseNpgsql(TestDataSource);
            options.AddInterceptors(new DatabaseIntegritySaveChangesInterceptor(signer));
            Context = new(options.Options, _fieldProtector);
            _transaction = await Context.Database.BeginTransactionAsync();
            Users = Enumerable.Range(0, 3).Select(index => new User
            {
                Username = $"owner-{index}", PasswordPhc = "test", WebDavTokenPhc = "test"
            }).ToArray();
            Context.Users.AddRange(Users);
        }

        [TearDown]
        public async Task RollBackFixture()
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            await Context.DisposeAsync();
            _keys.Dispose();
            _fieldProtector.Dispose();
        }

        protected async Task AssertDeletionAsync(object[] removed, Func<Task<int>> delete, bool repeat = true)
        {
            await Context.SaveChangesAsync();
            HashSet<string> removedKeys = removed.Select(entity => Key(Context.Entry(entity))).ToHashSet();
            Dictionary<string, PropertyValues> before = await SnapshotAsync();
            await _transaction.CreateSavepointAsync("before_delete");

            Assert.That(await delete(), Is.EqualTo(removedKeys.Count));
            if (repeat)
            {
                Assert.That(await delete(), Is.Zero);
            }
            Dictionary<string, PropertyValues> after = await SnapshotAsync();
            AssertSnapshot(before, after, before.Keys.Except(removedKeys));

            await _transaction.RollbackToSavepointAsync("before_delete");
            AssertSnapshot(before, await SnapshotAsync(), before.Keys);
        }

        private static string Key(EntityEntry entry)
        {
            return entry.Metadata.Name + ":" + string.Join("/", entry.Metadata.FindPrimaryKey()!.Properties.Select(property =>
                entry.Property(property.Name).CurrentValue is byte[] bytes
                    ? Convert.ToHexString(bytes)
                    : entry.Property(property.Name).CurrentValue!.ToString()));
        }

        private async Task<Dictionary<string, PropertyValues>> SnapshotAsync()
        {
            Context.ChangeTracker.Clear();
            await Context.Users.LoadAsync();
            await Context.UserLayouts.LoadAsync();
            await Context.Nodes.LoadAsync();
            await Context.NodeFiles.LoadAsync();
            await Context.FileManifests.LoadAsync();
            await Context.FileEmbeddings.LoadAsync();
            await Context.Chunks.LoadAsync();
            await Context.ChunkOwnerships.LoadAsync();
            await Context.DownloadTokens.LoadAsync();
            await Context.NodeShareTokens.LoadAsync();
            await Context.Notifications.LoadAsync();
            await Context.UserPasskeyCredentials.LoadAsync();
            await Context.UserExternalIdentities.LoadAsync();
            await Context.OidcProviders.LoadAsync();
            await Context.OidcLoginStates.LoadAsync();
            await Context.RefreshTokens.LoadAsync();
            await Context.SyncChanges.LoadAsync();
            Dictionary<string, PropertyValues> values = [];
            foreach (EntityEntry entry in Context.ChangeTracker.Entries())
            {
                if (_descriptors.TryGet(entry.Metadata.ClrType, out IDatabaseIntegrityDescriptor descriptor))
                {
                    byte[] signature = (byte[])entry.Property(DatabaseIntegrityColumns.MacProperty).CurrentValue!;
                    Assert.That(_protector.Verify(entry.Entity, descriptor, signature), Is.True, Key(entry));
                }
                values.Add(Key(entry), entry.CurrentValues.Clone());
            }
            return values;
        }

        private static void AssertSnapshot(Dictionary<string, PropertyValues> before,
            Dictionary<string, PropertyValues> after, IEnumerable<string> expectedKeys)
        {
            Assert.That(after.Keys, Is.EquivalentTo(expectedKeys));
            foreach ((string key, PropertyValues values) in after)
            {
                foreach (Microsoft.EntityFrameworkCore.Metadata.IProperty property in values.Properties)
                {
                    Assert.That(values[property], Is.EqualTo(before[key][property]), $"{key}.{property.Name}");
                }
            }
        }

        protected Layout AddLayout(User owner)
        {
            return Context.UserLayouts.Add(new Layout { Owner = owner }).Entity;
        }

        protected Node AddNode(User owner, Layout layout, string name)
        {
            Node node = new() { Owner = owner, Layout = layout };
            node.SetName(name);
            return Context.Nodes.Add(node).Entity;
        }

        protected FileManifest AddManifest()
        {
            FileManifest manifest = new() { ProposedContentHash = SHA256.HashData(Guid.NewGuid().ToByteArray()), SizeBytes = 100 };
            Context.FileManifests.Add(manifest);
            Context.Entry(manifest).Property<string>("ContentType").CurrentValue = string.Empty;
            return manifest;
        }

        protected NodeFile AddFile(User owner, Node node, FileManifest manifest, string name)
        {
            NodeFile file = new() { Owner = owner, Node = node, FileManifest = manifest };
            file.SetName(name);
            Context.NodeFiles.Add(file);
            file.OriginalNodeFileId = file.Id;
            return file;
        }

        protected Chunk AddChunk()
        {
            return Context.Chunks.Add(new Chunk
            {
                Hash = SHA256.HashData(Guid.NewGuid().ToByteArray()), PlainSizeBytes = 100, StoredSizeBytes = 80
            }).Entity;
        }

        protected OidcProvider AddProvider(string slug)
        {
            return Context.OidcProviders.Add(new OidcProvider
            {
                Name = slug, Slug = slug, Issuer = "https://issuer.example.test", ClientId = "client"
            }).Entity;
        }
    }
}
