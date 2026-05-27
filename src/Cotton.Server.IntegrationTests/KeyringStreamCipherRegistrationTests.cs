// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Server.Extensions;
using Cotton.Server.Services.KeyManagement;
using EasyExtensions.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Text;

namespace Cotton.Server.IntegrationTests;

public class KeyringStreamCipherRegistrationTests
{
    [Test]
    public async Task AddStreamCipher_UsesKeyringPrimaryKey_WhenBootstrapResultIsRegistered()
    {
        CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        KeyringPlainState state = KeyringV1UpgradeBuilder.CreateInitialState(
            settings,
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            keyringId: "test-keyring",
            createdAtUtc: DateTimeOffset.Parse("2026-05-27T00:00:00Z"));
        using ServiceProvider provider = BuildProvider(settings, CreateBootstrapResult(state));
        IStreamCipher cipher = provider.GetRequiredService<IStreamCipher>();

        byte[] encrypted = await EncryptAsync(cipher, "keyring registration");

        Assert.That(ReadKeyId(encrypted), Is.EqualTo(KeyringV1UpgradeBuilder.FirstV2ChunkKeyId));
    }

    [Test]
    public async Task AddStreamCipher_KeepsLegacyCipher_WhenNoKeyringIsRegistered()
    {
        CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        using ServiceProvider provider = BuildProvider(settings, keyring: null);
        IStreamCipher cipher = provider.GetRequiredService<IStreamCipher>();

        byte[] encrypted = await EncryptAsync(cipher, "legacy registration");

        Assert.That(ReadKeyId(encrypted), Is.EqualTo(ConfigurationBuilderExtensions.DefaultMasterKeyId));
    }

    private static ServiceProvider BuildProvider(
        CottonEncryptionSettings settings,
        KeyringBootstrapResult? keyring)
    {
        var services = new ServiceCollection();
        services.AddSingleton(settings);
        if (keyring is not null)
        {
            services.AddSingleton(keyring);
        }

        services.AddStreamCipher();
        return services.BuildServiceProvider();
    }

    private static KeyringBootstrapResult CreateBootstrapResult(KeyringPlainState state)
    {
        DateTimeOffset now = DateTimeOffset.Parse("2026-05-27T00:00:00Z");
        var access = new KeyringAccessEnvelope(
            KeyringFormat.AccessEnvelopeMagic,
            state.InstanceId,
            state.KeyringId,
            state.RootEpoch,
            Generation: 1,
            ParentHash: null,
            CreatedAtUtc: now,
            Recipients: []);
        var statePointer = new KeyringObjectPointer(
            KeyringObjectKind.StateSnapshot,
            state.StateGeneration,
            Hash: new string('0', 64),
            ObjectName: "state",
            CommittedAtUtc: now);
        var accessPointer = new KeyringObjectPointer(
            KeyringObjectKind.AccessEnvelope,
            Generation: 1,
            Hash: new string('0', 64),
            ObjectName: "access",
            CommittedAtUtc: now);

        return new KeyringBootstrapResult(
            Created: false,
            State: state,
            AccessEnvelope: access,
            StatePointer: statePointer,
            AccessPointer: accessPointer,
            UnlockedSlotId: null);
    }

    private static async Task<byte[]> EncryptAsync(IStreamCipher cipher, string text)
    {
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes(text), writable: false);
        await using var encrypted = new MemoryStream();
        await cipher.EncryptAsync(input, encrypted, leaveInputOpen: true, leaveOutputOpen: true);
        return encrypted.ToArray();
    }

    private static int ReadKeyId(byte[] encrypted)
    {
        return BitConverter.ToInt32(encrypted.AsSpan(16, 4));
    }
}
