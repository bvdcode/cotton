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

        [Test]
        public async Task ValidateOrInitializeAsync_Requires_Evidence_Before_Creating_Sentinel()
        {
            var probe = new DelegateCompatibilityProbe((_, mode) =>
                mode == MasterKeyCompatibilityMode.RequireEvidenceForExistingData
                    ? MasterKeyCompatibilityResult.Fail("probe required")
                    : MasterKeyCompatibilityResult.Compatible(existingDataFound: true, evidenceFound: false));
            var store = CreateStore(probe);
            CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

            MasterKeySentinelResult result = await store.ValidateOrInitializeAsync(
                settings,
                MasterKeySentinelInitializationMode.RequireCompatibilityEvidenceForExistingData);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Error, Is.EqualTo("probe required"));
                Assert.That(await store.ExistsAsync(), Is.False);
            }
        }

        [Test]
        public async Task ValidateOrInitializeAsync_Repairs_Sentinel_When_Data_Proves_Submitted_Key()
        {
            CottonEncryptionSettings wrong = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
            CottonEncryptionSettings correct = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            await CreateStore().ValidateOrInitializeAsync(wrong);

            var probe = new DelegateCompatibilityProbe((_, mode) =>
                mode == MasterKeyCompatibilityMode.RequireEvidenceForExistingData
                    ? MasterKeyCompatibilityResult.Compatible(existingDataFound: true, evidenceFound: true)
                    : MasterKeyCompatibilityResult.Compatible(existingDataFound: true, evidenceFound: false));
            var store = CreateStore(probe);

            MasterKeySentinelResult repaired = await store.ValidateOrInitializeAsync(correct);
            MasterKeySentinelResult reused = await store.ValidateOrInitializeAsync(correct);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(repaired.Success, Is.True);
                Assert.That(repaired.Created, Is.True);
                Assert.That(repaired.Repaired, Is.True);
                Assert.That(reused.Success, Is.True);
                Assert.That(reused.Created, Is.False);
            }
        }

        [Test]
        public async Task ValidateOrInitializeAsync_Does_Not_Repair_Sentinel_Without_Compatibility_Evidence()
        {
            CottonEncryptionSettings wrong = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
            CottonEncryptionSettings correct = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            await CreateStore().ValidateOrInitializeAsync(wrong);

            var probe = new DelegateCompatibilityProbe((_, _) =>
                MasterKeyCompatibilityResult.Compatible(existingDataFound: false, evidenceFound: false));
            var store = CreateStore(probe);

            MasterKeySentinelResult rejected = await store.ValidateOrInitializeAsync(correct);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(rejected.Success, Is.False);
                Assert.That(rejected.Error, Does.Contain("Master key"));
            }
        }

        private MasterKeySentinelStore CreateStore(IMasterKeyCompatibilityProbe? compatibilityProbe = null) =>
            new(NullLogger<MasterKeySentinelStore>.Instance, _storageBasePath, compatibilityProbe);

        private sealed class DelegateCompatibilityProbe(
            Func<CottonEncryptionSettings, MasterKeyCompatibilityMode, MasterKeyCompatibilityResult> _validate)
            : IMasterKeyCompatibilityProbe
        {
            public Task<MasterKeyCompatibilityResult> ValidateAsync(
                CottonEncryptionSettings encryptionSettings,
                MasterKeyCompatibilityMode mode,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(_validate(encryptionSettings, mode));
        }
    }
}
