// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Controllers;
using Cotton.Server.Handlers.Server;
using Cotton.Server.IntegrationTests.Helpers;
using EasyExtensions.Mediator;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Net;

namespace Cotton.Server.IntegrationTests
{
    public class VectorExtensionEndpointTests
    {
        private const string Endpoint = Routes.V1.Server + "/database/extensions/vector";

        [TestCase(null, HttpStatusCode.Unauthorized, 0)]
        [TestCase(nameof(UserRole.User), HttpStatusCode.Forbidden, 0)]
        [TestCase(nameof(UserRole.Admin), HttpStatusCode.OK, 1)]
        public async Task Patch_RequiresAdministrator(string? role, HttpStatusCode expectedStatus, int expectedCommands)
        {
            DbContextOptions<CottonDbContext> options = new DbContextOptionsBuilder<CottonDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused")
                .ReplaceService<IMigrationCommandExecutor, RecordingMigrationCommandExecutor>()
                .Options;
            await using CottonDbContext context = new(options);
            await using WebApplication application = await CreateApplicationAsync(context);
            using HttpClient client = application.GetTestClient();
            if (role is not null)
            {
                client.DefaultRequestHeaders.Add(VectorEndpointTestAuthenticationHandler.RoleHeader, role);
            }

            using HttpResponseMessage response = await client.PatchAsync(Endpoint, null);

            Assert.That(response.StatusCode, Is.EqualTo(expectedStatus));
            RecordingMigrationCommandExecutor executor =
                (RecordingMigrationCommandExecutor)context.GetService<IMigrationCommandExecutor>();
            Assert.That(executor.Commands, Has.Count.EqualTo(expectedCommands));
        }

        private static async Task<WebApplication> CreateApplicationAsync(CottonDbContext context)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = "Cotton.Server",
                EnvironmentName = "Testing"
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton(context);
            builder.Services.AddMediator();
            builder.Services.AddTransient<IRequestHandler<EnsureVectorExtensionRequest>, EnsureVectorExtensionRequestHandler>();
            builder.Services.AddAuthentication(VectorEndpointTestAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, VectorEndpointTestAuthenticationHandler>(
                    VectorEndpointTestAuthenticationHandler.SchemeName, _ => { });
            builder.Services.AddAuthorization();
            builder.Services.AddControllers().AddControllersAsServices();
            builder.Services.AddTransient(provider => new ServerController(
                provider.GetRequiredService<IMediator>(), null!, null!, null!));

            WebApplication application = builder.Build();
            application.UseRouting();
            application.UseAuthentication();
            application.UseAuthorization();
            application.MapControllers();
            await application.StartAsync();
            return application;
        }
    }
}
