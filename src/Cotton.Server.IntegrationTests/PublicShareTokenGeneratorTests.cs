// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class PublicShareTokenGeneratorTests
    {
        [Test]
        public void CreateForActiveShareCount_UsesCompactFormatBelowLimit()
        {
            string token = PublicShareTokenGenerator.CreateForActiveShareCount(
                PublicShareTokenGenerator.CompactTokenActiveShareLimit - 1);

            Assert.That(token, Does.Match("^[a-z0-9]{8}$"));
        }

        [Test]
        public void CreateForActiveShareCount_UsesExpandedFormatAtLimit()
        {
            string token = PublicShareTokenGenerator.CreateForActiveShareCount(
                PublicShareTokenGenerator.CompactTokenActiveShareLimit);

            Assert.That(token, Does.Match("^[a-zA-Z0-9]{12}$"));
        }
    }
}
