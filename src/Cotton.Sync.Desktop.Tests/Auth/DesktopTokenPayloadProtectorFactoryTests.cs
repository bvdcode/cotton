// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Auth;

namespace Cotton.Sync.Desktop.Tests.Auth
{
    public class DesktopTokenPayloadProtectorFactoryTests
    {
        private string _tempDirectory = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "cotton-secret-tool-path-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        [Test]
        public void CreateLinuxDefault_ReturnsSecretServiceProtectorWhenSecretToolExists()
        {
            string secretToolPath = Path.Combine(_tempDirectory, "secret-tool");
            File.WriteAllText(secretToolPath, string.Empty);

            ITokenPayloadProtector protector = DesktopTokenPayloadProtectorFactory.CreateLinuxDefault(_tempDirectory);

            Assert.Multiple(() =>
            {
                Assert.That(protector, Is.TypeOf<LinuxSecretServiceTokenPayloadProtector>());
                Assert.That(protector.Scheme, Is.EqualTo("linux-secret-service-v1"));
            });
        }

        [Test]
        public void CreateLinuxDefault_ReturnsUnsupportedProtectorWhenSecretToolIsMissing()
        {
            ITokenPayloadProtector protector = DesktopTokenPayloadProtectorFactory.CreateLinuxDefault(_tempDirectory);

            Assert.Multiple(() =>
            {
                Assert.That(protector, Is.TypeOf<UnsupportedTokenPayloadProtector>());
                Assert.That(protector.Scheme, Is.EqualTo("linux-secret-service-unavailable-v1"));
            });
        }

        [Test]
        public void ResolveExecutablePath_ReturnsCommandFromPath()
        {
            string secretToolPath = Path.Combine(_tempDirectory, "secret-tool");
            File.WriteAllText(secretToolPath, string.Empty);

            string? result = DesktopTokenPayloadProtectorFactory.ResolveExecutablePath("secret-tool", _tempDirectory);

            Assert.That(result, Is.EqualTo(secretToolPath));
        }

        [Test]
        public void ResolveExecutablePath_ReturnsNullWhenCommandIsMissing()
        {
            string? result = DesktopTokenPayloadProtectorFactory.ResolveExecutablePath("secret-tool", _tempDirectory);

            Assert.That(result, Is.Null);
        }
    }
}
