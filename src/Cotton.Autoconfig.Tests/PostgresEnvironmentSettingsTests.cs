// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Autoconfig.Tests
{
    public class PostgresEnvironmentSettingsTests
    {
        private const string HostEnvironmentVariable = "COTTON_PG_HOST";
        private const string PortEnvironmentVariable = "COTTON_PG_PORT";
        private const string DatabaseEnvironmentVariable = "COTTON_PG_DATABASE";
        private const string UsernameEnvironmentVariable = "COTTON_PG_USERNAME";
        private const string PasswordEnvironmentVariable = "COTTON_PG_PASSWORD";

        private static readonly string[] EnvironmentVariables =
        [
            HostEnvironmentVariable,
            PortEnvironmentVariable,
            DatabaseEnvironmentVariable,
            UsernameEnvironmentVariable,
            PasswordEnvironmentVariable,
        ];

        private readonly Dictionary<string, string?> _savedValues = [];

        [SetUp]
        public void SetUp()
        {
            foreach (string name in EnvironmentVariables)
            {
                _savedValues[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, null);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach ((string name, string? value) in _savedValues)
            {
                Environment.SetEnvironmentVariable(name, value);
            }

            _savedValues.Clear();
        }

        [Test]
        public void FromEnvironment_UsesDocumentedDefaults()
        {
            PostgresEnvironmentSettings settings = PostgresEnvironmentSettings.FromEnvironment();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(settings.Host, Is.EqualTo("localhost"));
                Assert.That(settings.Port, Is.EqualTo(5432));
                Assert.That(settings.Database, Is.EqualTo("cotton_dev"));
                Assert.That(settings.Username, Is.EqualTo("postgres"));
                Assert.That(settings.Password, Is.EqualTo("postgres"));
            }
        }

        [Test]
        public void FromEnvironment_ReturnsConfiguredValues()
        {
            Environment.SetEnvironmentVariable(HostEnvironmentVariable, "database.example");
            Environment.SetEnvironmentVariable(PortEnvironmentVariable, " 6432 ");
            Environment.SetEnvironmentVariable(DatabaseEnvironmentVariable, "cotton");
            Environment.SetEnvironmentVariable(UsernameEnvironmentVariable, "cotton-user");
            Environment.SetEnvironmentVariable(PasswordEnvironmentVariable, " password with spaces ");

            PostgresEnvironmentSettings settings = PostgresEnvironmentSettings.FromEnvironment();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(settings.Host, Is.EqualTo("database.example"));
                Assert.That(settings.Port, Is.EqualTo(6432));
                Assert.That(settings.Database, Is.EqualTo("cotton"));
                Assert.That(settings.Username, Is.EqualTo("cotton-user"));
                Assert.That(settings.Password, Is.EqualTo(" password with spaces "));
            }
        }

        [TestCase("")]
        [TestCase("not-a-port")]
        [TestCase("65536")]
        public void FromEnvironment_RejectsInvalidPort(string value)
        {
            Environment.SetEnvironmentVariable(PortEnvironmentVariable, value);

            InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(
                () => PostgresEnvironmentSettings.FromEnvironment());

            Assert.That(
                exception!.Message,
                Does.Contain(PortEnvironmentVariable));
        }
    }
}
