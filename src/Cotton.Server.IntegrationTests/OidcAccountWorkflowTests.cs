// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Auth.Oidc;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;

namespace Cotton.Server.IntegrationTests
{
    public class OidcAccountWorkflowTests : AuthEndpointTestBase
    {
        [Test]
        public async Task Resolve_CreatesAccountAndReusesLinkedIdentity()
        {
            await LoginAsync("admin", "testpassword");
            await using AsyncServiceScope scope = _customFactory!.Services.CreateAsyncScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            OidcProvider provider = await AddProviderAsync(db);
            OidcIdentityClaims claims = CreateClaims("external-user");

            User created = await mediator.Send(new ResolveOidcUserRequest(provider, claims));
            Guid userId = created.Id;
            db.ChangeTracker.Clear();
            provider = await db.OidcProviders.SingleAsync();
            User resolved = await mediator.Send(new ResolveOidcUserRequest(provider, claims with { GivenName = "Updated" }));
            await db.SaveChangesAsync();

            Assert.Multiple(() =>
            {
                Assert.That(resolved.Id, Is.EqualTo(userId));
                Assert.That(resolved.FirstName, Is.EqualTo("Updated"));
                Assert.That(resolved.Email, Is.EqualTo(claims.Email));
                Assert.That(resolved.IsEmailVerified, Is.True);
            });
            Assert.That(await db.UserExternalIdentities.CountAsync(), Is.EqualTo(1));
            Assert.That(await db.Users.CountAsync(x => x.Id == userId), Is.EqualTo(1));
        }

        [Test]
        public async Task Link_PreservesPreviousEmailAndRejectsAnotherOwner()
        {
            AuthSessionResponseDto login = await LoginAsync("owner", "testpassword");
            await using AsyncServiceScope scope = _customFactory!.Services.CreateAsyncScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            OidcProvider provider = await AddProviderAsync(db);
            User owner = await db.Users.SingleAsync(x => x.Id == login.User.Id);
            owner.Email = "previous@example.test";
            User other = new() { Username = "other", PasswordPhc = "phc", WebDavTokenPhc = "token" };
            db.Users.Add(other);
            await db.SaveChangesAsync();
            OidcIdentityClaims claims = CreateClaims("external-user");

            (User linked, string? previousEmail) = await mediator.Send(new LinkOidcIdentityRequest(owner.Id, provider, claims));
            await db.SaveChangesAsync();
            Assert.Multiple(() =>
            {
                Assert.That(linked.Id, Is.EqualTo(owner.Id));
                Assert.That(previousEmail, Is.EqualTo("previous@example.test"));
                Assert.That(linked.Email, Is.EqualTo(claims.Email));
            });
            Assert.ThrowsAsync<BadRequestException<UserExternalIdentity>>(
                async () => await mediator.Send(new LinkOidcIdentityRequest(other.Id, provider, claims)));
            Assert.That(await db.UserExternalIdentities.CountAsync(), Is.EqualTo(1));
        }

        [Test]
        public async Task Resolve_DisabledAccountCreationDoesNotCreateUsers()
        {
            await LoginAsync("admin", "testpassword");
            await using AsyncServiceScope scope = _customFactory!.Services.CreateAsyncScope();
            CottonDbContext db = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            OidcProvider provider = await AddProviderAsync(db);
            provider.AllowAccountCreation = false;
            await db.SaveChangesAsync();
            int usersBefore = await db.Users.CountAsync();

            Assert.ThrowsAsync<BadRequestException<OidcProvider>>(
                async () => await mediator.Send(new ResolveOidcUserRequest(provider, CreateClaims("blocked"))));

            Assert.That(await db.Users.CountAsync(), Is.EqualTo(usersBefore));
            Assert.That(await db.UserExternalIdentities.AnyAsync(), Is.False);
        }

        private static async Task<OidcProvider> AddProviderAsync(CottonDbContext db)
        {
            OidcProvider provider = new()
            {
                Name = "Test provider",
                Slug = "test-provider",
                Issuer = "https://identity.example.test",
                ClientId = "cotton",
                IsEnabled = true,
                AllowAccountCreation = true,
                RequireVerifiedEmail = true,
                SyncProfile = true,
                DefaultRole = UserRole.User,
            };
            db.OidcProviders.Add(provider);
            await db.SaveChangesAsync();
            return provider;
        }

        private static OidcIdentityClaims CreateClaims(string subject)
            => new("https://identity.example.test", subject, "user@example.test", true,
                "Test User", "Test", "User", null, "test-user");
    }
}
