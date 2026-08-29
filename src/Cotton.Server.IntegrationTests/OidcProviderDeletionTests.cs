// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Models.Dto;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using CottonLoginRequestDto = Cotton.Auth.LoginRequestDto;

namespace Cotton.Server.IntegrationTests
{
    public class OidcProviderDeletionTests : IntegrationTestBase
    {
        private TestAppFactory? _factory;
        private HttpClient? _client;

        [SetUp]
        public void SetUp()
        {
            IRelationalDatabaseCreator creator = DbContext.GetService<IRelationalDatabaseCreator>();
            creator.EnsureDeleted();
            creator.Create();

            NpgsqlConnectionStringBuilder connection = new NpgsqlConnectionStringBuilder
            {
                Host = TestPostgresHost,
                Port = TestPostgresPort,
                Database = CurrentDatabaseName,
                Username = TestPostgresUsername,
                Password = TestPostgresPassword,
            };
            Dictionary<string, string?> overrides = new Dictionary<string, string?>
            {
                ["DatabaseSettings:Host"] = connection.Host,
                ["DatabaseSettings:Port"] = connection.Port.ToString(),
                ["DatabaseSettings:Database"] = connection.Database,
                ["DatabaseSettings:Username"] = connection.Username,
                ["DatabaseSettings:Password"] = connection.Password,
                ["MasterEncryptionKey"] = Convert.ToBase64String(
                    SHA256.HashData(Encoding.UTF8.GetBytes("super"))),
                ["MasterEncryptionKeyId"] = "1",
                ["EncryptionThreads"] = "1",
                ["MaxChunkSizeBytes"] = "16777216",
                ["CipherChunkSizeBytes"] = "20971520",
                ["JwtSettings:Key"] = "T3wNTuKqmTXKjJKXHJRGUpG9sdrmpSX4",
            };

            _factory = new TestAppFactory(overrides);
            _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });
        }

        [TearDown]
        public void TearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }

        [Test]
        public async Task DeleteProvider_RemovesRestrictedDependents()
        {
            string adminToken = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            UserDto? currentUser = await _client.GetFromJsonAsync<UserDto>("/api/v1/users/me");
            Assert.That(currentUser, Is.Not.Null);
            Guid providerId = await SeedProviderAsync(currentUser!.Id);

            using HttpResponseMessage deleteResponse = await _client.DeleteAsync(
                $"/api/v1/auth/oidc/providers/{providerId}");

            Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            await AssertProviderRemovedAsync(providerId);
        }

        private async Task<Guid> SeedProviderAsync(Guid userId)
        {
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            OidcProvider provider = new OidcProvider
            {
                Name = "Restricted deletion provider",
                Slug = $"restricted-delete-{Guid.NewGuid():N}",
                Issuer = "https://restricted-delete.example.com",
                ClientId = "restricted-delete-client",
                Scopes = ["openid"],
                DefaultRole = UserRole.User,
            };
            dbContext.OidcProviders.Add(provider);
            dbContext.UserExternalIdentities.Add(new UserExternalIdentity
            {
                UserId = userId,
                Provider = provider,
                Issuer = provider.Issuer,
                Subject = "restricted-delete-subject",
            });
            dbContext.OidcLoginStates.Add(new OidcLoginState
            {
                Provider = provider,
                StateHash = Guid.NewGuid().ToString("N"),
                CodeVerifierEncrypted = "restricted-delete-verifier",
                NonceEncrypted = "restricted-delete-nonce",
                ReturnUrl = "/",
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            });
            await dbContext.SaveChangesAsync();
            return provider.Id;
        }

        private async Task AssertProviderRemovedAsync(Guid providerId)
        {
            await using AsyncServiceScope scope = _factory!.Services.CreateAsyncScope();
            CottonDbContext dbContext = scope.ServiceProvider.GetRequiredService<CottonDbContext>();
            bool providerExists = await dbContext.OidcProviders
                .AnyAsync(provider => provider.Id == providerId);
            bool identityExists = await dbContext.UserExternalIdentities
                .AnyAsync(identity => identity.ProviderId == providerId);
            bool loginStateExists = await dbContext.OidcLoginStates
                .AnyAsync(state => state.ProviderId == providerId);

            Assert.Multiple(() =>
            {
                Assert.That(providerExists, Is.False);
                Assert.That(identityExists, Is.False);
                Assert.That(loginStateExists, Is.False);
            });
        }

        private async Task<string> LoginAsync()
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
            {
                Content = JsonContent.Create(new CottonLoginRequestDto
                {
                    Username = "testuser",
                    Password = "testpassword",
                }),
            };
            request.Headers.Add("X-Forwarded-For", "8.8.8.8");
            using HttpResponseMessage response = await _client!.SendAsync(request);
            response.EnsureSuccessStatusCode();
            TokenPairResponseDto? login = await response.Content.ReadFromJsonAsync<TokenPairResponseDto>();
            Assert.That(login, Is.Not.Null);
            return login!.AccessToken;
        }
    }
}
