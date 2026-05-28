// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Crypto;
using Cotton.Server.Services.KeyManagement;
using NUnit.Framework;
using System.Text;

namespace Cotton.Server.IntegrationTests;

public class KeyringStreamCipherTests
{
    [Test]
    public async Task EncryptAsync_WritesWithPrimaryV2Key_AndDecryptsByHeaderKeyId()
    {
        KeyringPlainState state = CreateState(out _);
        var resolver = new KeyringPlainStateKeyResolver(state);
        var cipher = new KeyringStreamCipher(resolver);
        byte[] plaintext = Encoding.UTF8.GetBytes("hello keyring v2");

        await using var input = new MemoryStream(plaintext, writable: false);
        await using var encrypted = new MemoryStream();
        await cipher.EncryptAsync(input, encrypted, leaveInputOpen: true, leaveOutputOpen: true);
        byte[] encryptedBytes = encrypted.ToArray();
        int keyId = ReadKeyId(encryptedBytes);
        await using Stream decrypted = await cipher.DecryptAsync(new MemoryStream(encryptedBytes), leaveOpen: false);
        using var output = new MemoryStream();
        await decrypted.CopyToAsync(output);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(keyId, Is.EqualTo(KeyringV1UpgradeBuilder.FirstV2ChunkKeyId));
            Assert.That(output.ToArray(), Is.EqualTo(plaintext));
        }
    }

    [Test]
    public async Task DecryptAsync_ReadsLegacyV1KeyId()
    {
        KeyringPlainState state = CreateState(out CottonEncryptionSettings settings);
        var resolver = new KeyringPlainStateKeyResolver(state);
        var keyringCipher = new KeyringStreamCipher(resolver);
        byte[] plaintext = Encoding.UTF8.GetBytes("legacy data");
        byte[] legacyMaterial = Convert.FromBase64String(settings.MasterEncryptionKey);

        await using var input = new MemoryStream(plaintext, writable: false);
        await using var encrypted = new MemoryStream();
        using var legacyCipher = new AesGcmStreamCipher(legacyMaterial, KeyringV1UpgradeBuilder.LegacyKeyId);
        await legacyCipher.EncryptAsync(input, encrypted, leaveInputOpen: true, leaveOutputOpen: true);
        await using Stream decrypted = await keyringCipher.DecryptAsync(new MemoryStream(encrypted.ToArray()), leaveOpen: false);
        using var output = new MemoryStream();
        await decrypted.CopyToAsync(output);

        Assert.That(output.ToArray(), Is.EqualTo(plaintext));
    }

    [Test]
    public async Task ReencryptLegacyStream_WritesPrimaryV2Key_AndPreservesPlaintext()
    {
        KeyringPlainState state = CreateState(out CottonEncryptionSettings settings);
        var resolver = new KeyringPlainStateKeyResolver(state);
        var keyringCipher = new KeyringStreamCipher(resolver);
        byte[] plaintext = Encoding.UTF8.GetBytes("legacy chunk to rewrite");
        byte[] legacyMaterial = Convert.FromBase64String(settings.MasterEncryptionKey);

        await using var input = new MemoryStream(plaintext, writable: false);
        await using var legacyEncrypted = new MemoryStream();
        using (var legacyCipher = new AesGcmStreamCipher(legacyMaterial, KeyringV1UpgradeBuilder.LegacyKeyId))
        {
            await legacyCipher.EncryptAsync(input, legacyEncrypted, leaveInputOpen: true, leaveOutputOpen: true);
        }

        await using Stream decrypted = await keyringCipher.DecryptAsync(
            new MemoryStream(legacyEncrypted.ToArray()),
            leaveOpen: false);
        await using var rewritten = new MemoryStream();
        await keyringCipher.EncryptAsync(
            decrypted,
            rewritten,
            leaveInputOpen: true,
            leaveOutputOpen: true);
        byte[] rewrittenBytes = rewritten.ToArray();
        await using Stream restored = await keyringCipher.DecryptAsync(
            new MemoryStream(rewrittenBytes),
            leaveOpen: false);
        using var output = new MemoryStream();
        await restored.CopyToAsync(output);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ReadKeyId(legacyEncrypted.ToArray()), Is.EqualTo(KeyringV1UpgradeBuilder.LegacyKeyId));
            Assert.That(ReadKeyId(rewrittenBytes), Is.EqualTo(KeyringV1UpgradeBuilder.FirstV2ChunkKeyId));
            Assert.That(output.ToArray(), Is.EqualTo(plaintext));
        }
    }

    [Test]
    public async Task DecryptAsync_FailsClosed_ForUnknownKeyId()
    {
        KeyringPlainState state = CreateState(out _);
        var resolver = new KeyringPlainStateKeyResolver(state);
        var keyringCipher = new KeyringStreamCipher(resolver);
        byte[] plaintext = Encoding.UTF8.GetBytes("unknown key id");
        byte[] unknownKey = KeyringCryptography.GenerateKeyMaterial();
        try
        {
            await using var input = new MemoryStream(plaintext, writable: false);
            await using var encrypted = new MemoryStream();
            using var unknownCipher = new AesGcmStreamCipher(unknownKey, keyId: 777);
            await unknownCipher.EncryptAsync(input, encrypted, leaveInputOpen: true, leaveOutputOpen: true);
            byte[] encryptedBytes = encrypted.ToArray();

            Assert.That(
                async () => await keyringCipher.DecryptAsync(new MemoryStream(encryptedBytes), leaveOpen: false),
                Throws.TypeOf<KeyNotFoundException>());
        }
        finally
        {
            Array.Clear(unknownKey);
        }
    }

    private static KeyringPlainState CreateState(out CottonEncryptionSettings settings)
    {
        settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        return KeyringV1UpgradeBuilder.CreateInitialState(
            settings,
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            keyringId: "test-keyring",
            createdAtUtc: DateTimeOffset.Parse("2026-05-27T00:00:00Z"));
    }

    private static int ReadKeyId(byte[] encrypted)
    {
        return BitConverter.ToInt32(encrypted.AsSpan(16, 4));
    }
}
