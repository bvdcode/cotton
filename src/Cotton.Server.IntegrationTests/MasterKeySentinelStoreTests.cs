using Cotton.Autoconfig.Extensions;
using Cotton.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class MasterKeySentinelStoreTests
    {
        private string _storageBasePath = null!;

        [SetUp]
        public void SetUp()
        {
            _storageBasePath = Path.Combine(Path.GetTempPath(), "cotton-sentinel-tests", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_storageBasePath))
            {
                Directory.Delete(_storageBasePath, recursive: true);
            }
        }

        [Test]
        public async Task ValidateOrInitializeAsync_Creates_And_Reuses_Sentinel()
        {
            var store = CreateStore();
            CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

            MasterKeySentinelResult created = await store.ValidateOrInitializeAsync(settings);
            MasterKeySentinelResult reused = await store.ValidateOrInitializeAsync(settings);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(created.Success, Is.True);
                Assert.That(created.Created, Is.True);
                Assert.That(reused.Success, Is.True);
                Assert.That(reused.Created, Is.False);
            }
        }

        [Test]
        public async Task ValidateOrInitializeAsync_Rejects_Wrong_MasterKey()
        {
            var store = CreateStore();
            CottonEncryptionSettings original = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            CottonEncryptionSettings wrong = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

            await store.ValidateOrInitializeAsync(original);
            MasterKeySentinelResult rejected = await store.ValidateOrInitializeAsync(wrong);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(rejected.Success, Is.False);
                Assert.That(rejected.Error, Does.Contain("Master key"));
            }
        }

        private MasterKeySentinelStore CreateStore() =>
            new(NullLogger<MasterKeySentinelStore>.Instance, _storageBasePath);
    }
}
