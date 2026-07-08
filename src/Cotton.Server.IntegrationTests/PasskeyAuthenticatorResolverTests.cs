// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services.Passkeys;
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
        public void ResolveDefaultNamePrefersKnownProvider()
        {
            Guid aaGuid = Guid.Parse("b7d3f68e-88a6-471e-9ecf-2df26d041ede");

            string name = PasskeyAuthenticatorResolver.ResolveDefaultName(aaGuid, ["usb"]);

            Assert.That(name, Is.EqualTo("Security Key NFC by Yubico"));
        }

        [Test]
        public void ResolveDefaultNameFallsBackToSecurityKeyTransport()
        {
            string name = PasskeyAuthenticatorResolver.ResolveDefaultName(Guid.NewGuid(), ["nfc"]);

            Assert.That(name, Is.EqualTo("Security key"));
        }

        [Test]
        public void ResolveDefaultNameFallsBackToSecurityKeyTransportForZeroAaGuid()
        {
            string name = PasskeyAuthenticatorResolver.ResolveDefaultName(Guid.Empty, ["usb", "nfc"]);

            Assert.That(name, Is.EqualTo("Security key"));
        }

        [Test]
        public void ResolveDefaultNameFallsBackToDeviceTransport()
        {
            string name = PasskeyAuthenticatorResolver.ResolveDefaultName(Guid.NewGuid(), ["internal"]);

            Assert.That(name, Is.EqualTo("Device passkey"));
        }

        [Test]
        public void ResolveDefaultNameFallsBackToGenericPasskey()
        {
            string name = PasskeyAuthenticatorResolver.ResolveDefaultName(Guid.NewGuid(), []);

            Assert.That(name, Is.EqualTo("Passkey"));
        }
    }
}
