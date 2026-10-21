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

            Assert.That(hash, Is.EqualTo("816419cd7f7e5d4ae62c433b94a8040160f2fe9a80d110f252da1f0410dbccbe"),
                "The approved bulk mutation contract changed. Two separate, explicit user confirmations "
                + "are required for each new member or changed operation before updating this snapshot. "
                + "Each mutation must prove that its updated fields are excluded from the actual integrity "
                + "descriptor and that stored signatures remain valid. Deletions must prove their exact scope "
                + "and preserve retained rows and signatures. Do not update the hash merely to pass this test.");
        }
    }
}
