// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Helpers;
using Cotton.Server.Models;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class TotpHelpersTests
    {
        [Test]
        public void CreateSetup_UsesCompatibleProvisioningUri()
        {
            TotpSetup setup = TotpHelpers.CreateSetup(
                "Cotton Cloud",
                "alice@example.com",
                new Uri("https://cloud.example.com/assets/icons/icon-192.png"));

            string expectedUri =
                $"otpauth://totp/Cotton%20Cloud:alice%40example.com" +
                $"?secret={setup.SecretBase32}" +
                "&issuer=Cotton%20Cloud&digits=6&period=30" +
                "&imagelink=https%3A%2F%2Fcloud.example.com%2Fassets%2Ficons%2Ficon-192.png";
            Assert.That(setup.OtpAuthUri, Is.EqualTo(expectedUri));
        }
    }
}
