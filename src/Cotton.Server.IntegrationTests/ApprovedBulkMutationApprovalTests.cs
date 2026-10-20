// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using NUnit.Framework;
using System.Security.Cryptography;
using System.Text;

namespace Cotton.Server.IntegrationTests
{
    public class ApprovedBulkMutationApprovalTests
    {
        [Test]
        public async Task Source_MatchesExplicitlyApprovedOperations()
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory,
                "ApprovalContracts", "ApprovedBulkMutationExtensions.cs");
            string source = (await File.ReadAllTextAsync(path)).ReplaceLineEndings("\n");
            string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source)));

            Assert.That(hash, Is.EqualTo("6e0a21cfce3a56d374b8b92dc79bc588711a2a44bb779bf964d6c50c5586982f"),
                "The approved bulk mutation contract changed. Two separate, explicit user confirmations "
                + "are required for each new member or changed operation before updating this snapshot. "
                + "Each mutation must prove that its updated fields are excluded from the actual integrity "
                + "descriptor and that stored signatures remain valid. Do not update the hash merely to pass this test.");
        }
    }
}
