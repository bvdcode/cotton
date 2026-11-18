// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Cotton.Server.IntegrationTests
{
    public class DatabaseUnavailableExceptionHandlerTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public async Task RecoveringDatabaseReturnsServiceUnavailableBeforeEndpointRuns(bool wrapped)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddExceptionHandler<DatabaseUnavailableExceptionHandler>();
            builder.Services.AddProblemDetails();
            await using WebApplication app = builder.Build();
            bool endpointRan = false;
            app.UseExceptionHandler();
            app.Use((HttpContext context, RequestDelegate next) =>
            {
                PostgresException databaseError = new(
                    "the database system is not yet accepting connections",
                    "FATAL", "FATAL", PostgresErrorCodes.CannotConnectNow,
                    detail: "Consistent recovery state has not been yet reached.");
                if (wrapped)
                {
                    throw new InvalidOperationException("A transient failure occurred.", databaseError);
                }

                throw databaseError;
            });
            app.Run(context =>
            {
                endpointRan = true;
                return Task.CompletedTask;
            });
            await app.StartAsync();
            using HttpClient client = app.GetTestClient();

            using HttpResponseMessage response = await client.PostAsync("/upload", new ByteArrayContent([1, 2, 3]));
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            Assert.Multiple(() =>
            {
                Assert.That(endpointRan, Is.False);
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
                Assert.That(response.Headers.RetryAfter?.Delta, Is.EqualTo(TimeSpan.FromSeconds(5)));
                Assert.That(body.RootElement.GetProperty("success").GetBoolean(), Is.False);
                Assert.That(body.RootElement.GetProperty("messageCode").GetString(), Is.EqualTo("database_temporarily_unavailable"));
                Assert.That(body.RootElement.GetProperty("message").GetString(), Does.Not.Contain("Consistent recovery"));
            });
        }

        [TestCase(PostgresErrorCodes.UniqueViolation)]
        [TestCase(PostgresErrorCodes.InsufficientPrivilege)]
        [TestCase(PostgresErrorCodes.UndefinedTable)]
        public async Task OtherDatabaseErrorsAreNotMarkedAsRetryable(string sqlState)
        {
            DatabaseUnavailableExceptionHandler handler = new(NullLogger<DatabaseUnavailableExceptionHandler>.Instance);
            DefaultHttpContext context = new();
            DbUpdateException error = new("Database write failed.", new PostgresException("failure", "ERROR", "ERROR", sqlState));

            bool handled = await handler.TryHandleAsync(context, error, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(handled, Is.False);
                Assert.That(context.Response.Headers.ContainsKey("Retry-After"), Is.False);
            });
        }

        [Test]
        public async Task UnknownCommitOutcomeIsNotMarkedAsRetryable()
        {
            DatabaseUnavailableExceptionHandler handler = new(NullLogger<DatabaseUnavailableExceptionHandler>.Instance);
            DefaultHttpContext context = new();
            NpgsqlException error = new("Connection lost during commit.", new IOException("Connection reset."));

            bool handled = await handler.TryHandleAsync(context, error, CancellationToken.None);

            Assert.That(handled, Is.False);
        }
    }
}
