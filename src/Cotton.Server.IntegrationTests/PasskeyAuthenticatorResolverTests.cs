// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services.Passkeys;
using Cotton.Server.Models.Enums;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class PasskeyAuthenticatorResolverTests
    {
        [Test]
        public void ResolveNameReturnsKnownPasskeyProvider()
        {
            Guid aaGuid = Guid.Parse("ea9b8d66-4d01-1d21-3ce4-b6b48cb575d4");

            string? name = PasskeyAuthenticatorResolver.ResolveName(aaGuid);

            Assert.That(name, Is.EqualTo("Google Password Manager"));
        }

        [Test]
        public void ResolveNameReturnsNullForUnknownProvider()
        {
            string? name = PasskeyAuthenticatorResolver.ResolveName(Guid.Empty);

            Assert.That(name, Is.Null);
        }

        [Test]
        public void ResolveDisplayNamePrefersKnownProvider()
        {
            Guid aaGuid = Guid.Parse("b7d3f68e-88a6-471e-9ecf-2df26d041ede");

            string name = PasskeyAuthenticatorResolver.ResolveDisplayName(aaGuid, ["usb"]);

            Assert.That(name, Is.EqualTo("Security Key NFC by Yubico"));
        }

        [Test]
        public void ResolveKindReturnsSecurityKeyForExternalTransport()
        {
            PasskeyAuthenticatorKind kind = PasskeyAuthenticatorResolver.ResolveKind(["nfc"]);

            Assert.That(kind, Is.EqualTo(PasskeyAuthenticatorKind.SecurityKey));
        }

        [Test]
        public void ResolveKindReturnsSecurityKeyForZeroAaGuidCredentialTransports()
        {
            PasskeyAuthenticatorKind kind = PasskeyAuthenticatorResolver.ResolveKind(["usb", "nfc"]);

            Assert.That(kind, Is.EqualTo(PasskeyAuthenticatorKind.SecurityKey));
        }

        [Test]
        public void ResolveKindReturnsDeviceForInternalTransport()
        {
            PasskeyAuthenticatorKind kind = PasskeyAuthenticatorResolver.ResolveKind(["internal"]);

            Assert.That(kind, Is.EqualTo(PasskeyAuthenticatorKind.Device));
        }

        [Test]
        public void ResolveKindReturnsUnknownWithoutRecognizedTransport()
        {
            PasskeyAuthenticatorKind kind = PasskeyAuthenticatorResolver.ResolveKind([]);

            Assert.That(kind, Is.EqualTo(PasskeyAuthenticatorKind.Unknown));
        }
    }
}
