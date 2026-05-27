// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Crypto;
using Cotton.Server.Services.KeyManagement;
using EasyExtensions.Abstractions;
using EasyExtensions.Extensions;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests;

public class KeyringPurposeCipherFactoryTests
{
    [Test]
    public void TotpCipher_EncryptsWithTotpPrimaryKey_AndDecryptsLegacyTotpSecret()
    {
        CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        KeyringPlainState state = KeyringV1UpgradeBuilder.CreateInitialState(
            settings,
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            keyringId: "test-keyring",
            createdAtUtc: DateTimeOffset.Parse("2026-05-27T00:00:00Z"));
        using var factory = new KeyringPurposeCipherFactory(settings, state);
        IStreamCipher totpCipher = factory.CreateTotpSecretCipher();

        byte[] encrypted = totpCipher.EncryptString("NEW-TOTP-SECRET");
        string decrypted = totpCipher.DecryptString(encrypted);
        byte[] legacyEncrypted = EncryptLegacyTotp(settings, "LEGACY-TOTP-SECRET");
        string legacyDecrypted = totpCipher.DecryptString(legacyEncrypted);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ReadKeyId(encrypted), Is.EqualTo(KeyringV1UpgradeBuilder.FirstV2TotpSecretKeyId));
            Assert.That(decrypted, Is.EqualTo("NEW-TOTP-SECRET"));
            Assert.That(ReadKeyId(legacyEncrypted), Is.EqualTo(KeyringV1UpgradeBuilder.LegacyKeyId));
            Assert.That(legacyDecrypted, Is.EqualTo("LEGACY-TOTP-SECRET"));
        }
    }

    [Test]
    public void DbFieldCipher_EncryptsWithDbFieldPrimaryKey_AndDecryptsLegacyDbField()
    {
        CottonEncryptionSettings settings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        KeyringPlainState state = KeyringV1UpgradeBuilder.CreateInitialState(
            settings,
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            keyringId: "test-keyring",
            createdAtUtc: DateTimeOffset.Parse("2026-05-27T00:00:00Z"));
        using var factory = new KeyringPurposeCipherFactory(settings, state);
        IStreamCipher dbFieldCipher = factory.CreateDbFieldCipher();
        byte[] fieldValue = [1, 2, 3, 4];

        byte[] encrypted = dbFieldCipher.Encrypt(fieldValue);
        byte[] decrypted = dbFieldCipher.Decrypt(encrypted);
        byte[] legacyEncrypted = EncryptLegacyBytes(settings, fieldValue);
        byte[] legacyDecrypted = dbFieldCipher.Decrypt(legacyEncrypted);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ReadKeyId(encrypted), Is.EqualTo(KeyringV1UpgradeBuilder.FirstV2DbFieldKeyId));
            Assert.That(decrypted, Is.EqualTo(fieldValue));
            Assert.That(ReadKeyId(legacyEncrypted), Is.EqualTo(KeyringV1UpgradeBuilder.LegacyKeyId));
            Assert.That(legacyDecrypted, Is.EqualTo(fieldValue));
        }
    }

    private static byte[] EncryptLegacyTotp(CottonEncryptionSettings settings, string secret)
    {
        byte[] material = Convert.FromBase64String(settings.MasterEncryptionKey);
        try
        {
            using var legacyCipher = new AesGcmStreamCipher(material, KeyringV1UpgradeBuilder.LegacyKeyId);
            return legacyCipher.EncryptString(secret);
        }
        finally
        {
            Array.Clear(material);
        }
    }

    private static byte[] EncryptLegacyBytes(CottonEncryptionSettings settings, byte[] value)
    {
        byte[] material = Convert.FromBase64String(settings.MasterEncryptionKey);
        try
        {
            using var legacyCipher = new AesGcmStreamCipher(material, KeyringV1UpgradeBuilder.LegacyKeyId);
            return legacyCipher.Encrypt(value);
        }
        finally
        {
            Array.Clear(material);
        }
    }

    private static int ReadKeyId(byte[] encrypted)
    {
        return BitConverter.ToInt32(encrypted.AsSpan(16, 4));
    }
}
