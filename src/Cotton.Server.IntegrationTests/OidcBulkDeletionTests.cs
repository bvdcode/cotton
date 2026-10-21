// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Extensions;

namespace Cotton.Server.IntegrationTests
{
    public class OidcBulkDeletionTests : BulkDeletionTestBase
    {
        [TestCase(false)]
        [TestCase(true)]
        public async Task IdentityDeletion_PreservesOtherUsersOrProviders(bool byProvider)
        {
            OidcProvider[] providers = [AddProvider("first"), AddProvider("second")];
            UserExternalIdentity[,] rows = new UserExternalIdentity[3, 2];
            for (int user = 0; user < 3; user++)
            {
                for (int provider = 0; provider < 2; provider++)
                {
                    rows[user, provider] = Context.UserExternalIdentities.Add(new UserExternalIdentity
                    {
                        User = Users[user], Provider = providers[provider], Issuer = providers[provider].Issuer,
                        Subject = Users[user].Username
                    }).Entity;
                }
            }
            if (byProvider)
            {
                await AssertDeletionAsync([rows[0, 0], rows[1, 0], rows[2, 0]],
                    () => Context.UserExternalIdentities.DeleteExternalIdentitiesForProviderAsync(providers[0].Id, CancellationToken.None));
            }
            else
            {
                await AssertDeletionAsync([rows[0, 0], rows[0, 1]],
                    () => Context.UserExternalIdentities.DeleteExternalIdentitiesForUserAsync(Users[0].Id, CancellationToken.None));
            }
        }

        [TestCase("user")]
        [TestCase("provider")]
        [TestCase("expired")]
        public async Task StateDeletion_PreservesUnselectedStatesAndProviders(string operation)
        {
            OidcProvider[] providers = [AddProvider("first"), AddProvider("second")];
            OidcLoginState[] states =
            [
                AddState(providers[0], Users[0].Id, DateTime.UtcNow.AddDays(-1)),
                AddState(providers[0], Users[1].Id, DateTime.UtcNow.AddDays(1)),
                AddState(providers[1], Users[0].Id, DateTime.UtcNow.AddDays(1)),
                AddState(providers[1], Users[1].Id, DateTime.UtcNow.AddDays(-1)),
                AddState(providers[1], null, DateTime.UtcNow.AddDays(1))
            ];
            switch (operation)
            {
                case "user":
                    await AssertDeletionAsync([states[0], states[2]],
                        () => Context.OidcLoginStates.DeleteOidcStatesForUserAsync(Users[0].Id, CancellationToken.None));
                    break;
                case "provider":
                    await AssertDeletionAsync([states[0], states[1]],
                        () => Context.OidcLoginStates.DeleteOidcStatesForProviderAsync(providers[0].Id, CancellationToken.None));
                    break;
                case "expired":
                    await AssertDeletionAsync([states[0], states[3]],
                        () => Context.OidcLoginStates.DeleteExpiredOidcStatesAsync(CancellationToken.None));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation));
            }
        }

        private OidcLoginState AddState(OidcProvider provider, Guid? userId, DateTime expires)
        {
            return Context.OidcLoginStates.Add(new OidcLoginState
            {
                Provider = provider, LinkUserId = userId, ExpiresAt = expires, StateHash = Guid.NewGuid().ToString("N"),
                CodeVerifierEncrypted = "verifier", NonceEncrypted = "nonce", ReturnUrl = "/"
            }).Entity;
        }
    }
}
