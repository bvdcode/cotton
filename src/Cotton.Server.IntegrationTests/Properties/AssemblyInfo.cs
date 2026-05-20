// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using NUnit.Framework;

// The integration suite uses process-wide environment variables and shared
// PostgreSQL databases in several fixtures. Keep this assembly on one worker so
// test hosts do not reset each other's database or master-key state.
[assembly: LevelOfParallelism(1)]

[SetUpFixture]
public sealed class IntegrationTestAssemblySetup
{
    private string? _previousReloadConfigOnChange;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _previousReloadConfigOnChange = Environment.GetEnvironmentVariable("DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE");
        Environment.SetEnvironmentVariable("DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE", "false");
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Environment.SetEnvironmentVariable("DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE", _previousReloadConfigOnChange);
    }
}
