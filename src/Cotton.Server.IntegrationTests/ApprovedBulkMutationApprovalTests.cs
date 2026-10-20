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

            Assert.That(hash, Is.EqualTo("6bac5b28d4d18ba76b4e9a87f25efad64423339ba82897d811adf143a48662d4"),
                "The approved bulk mutation contract changed. Two separate, explicit user confirmations "
                + "are required for each new member or changed operation before updating this snapshot. "
                + "Each mutation must prove that its updated fields are excluded from the actual integrity "
                + "descriptor and that stored signatures remain valid. Do not update the hash merely to pass this test.");
        }
    }
}
