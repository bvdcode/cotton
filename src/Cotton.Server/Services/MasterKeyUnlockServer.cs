// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Represents master key unlock server.
    /// </summary>
    public static class MasterKeyUnlockServer
    {
        private const string UnlockApiBase = Routes.V1.Base + "/unlock";
        private static readonly TimeSpan FirstUnlockWindow = TimeSpan.FromMinutes(Constants.AdminAutocreateMinutesDelay);

        /// <summary>
        /// Waits for for unlock.
        /// </summary>
        public static async Task<MasterKeyUnlockResult> WaitForUnlockAsync(string[] args)
        {
            var completion = new TaskCompletionSource<MasterKeyUnlockResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;
            DateTimeOffset firstUnlockExpiresAtUtc = startedAtUtc.Add(FirstUnlockWindow);
            string bootstrapToken = GenerateBootstrapToken();
            var builder = WebApplication.CreateBuilder(args);
            builder.Logging.AddFilter(
                "Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager",
                LogLevel.Error);

            var app = builder.Build();
            ILoggerFactory loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("Cotton.Server.Unlock");
            var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
            IWebHostEnvironment environment = app.Services.GetRequiredService<IWebHostEnvironment>();

            app.UseDefaultFiles();
            app.Use(async (context, next) =>
            {
                if (IsLockedApiRequest(context.Request) && !IsUnlockApiRequest(context.Request))
                {
                    await WriteLockedApiResponseAsync(context);
                    return;
                }

                await next();
            });
            app.MapStaticAssets();

            app.MapGet(UnlockApiBase + "/status", async (HttpContext context) =>
            {
                DisableCaching(context);
                bool requiresBootstrapToken = await RequiresBootstrapTokenAsync(
                    environment,
                    context.RequestAborted);
                return Results.Ok(new UnlockStatusResponse(
                    RequiresBootstrapToken: requiresBootstrapToken,
                    FirstUnlockExpiresAtUtc: requiresBootstrapToken ? firstUnlockExpiresAtUtc : null));
            });
            app.MapGet(UnlockApiBase + "/key", (HttpContext context) =>
            {
                DisableCaching(context);
                return Results.Text(GenerateRootMasterKey(), "text/plain; charset=utf-8");
            });
            app.MapPost(UnlockApiBase, async (HttpContext context) =>
            {
                DisableCaching(context);
                SubmittedUnlockRequest submitted = await ReadSubmittedUnlockRequestAsync(context);
                IResult? bootstrapError = await ValidateBootstrapTokenAsync(
                    environment,
                    submitted.BootstrapToken,
                    bootstrapToken,
                    firstUnlockExpiresAtUtc,
                    context.RequestAborted);
                if (bootstrapError is not null)
                {
                    return bootstrapError;
                }

                CottonEncryptionSettings encryptionSettings;
                try
                {
                    encryptionSettings = ConfigurationBuilderExtensions.DeriveEncryptionSettings(submitted.MasterKey ?? string.Empty);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new UnlockResponse(false, ex.Message));
                }

                MasterKeySentinelStore sentinel = MasterKeyStartupStorage.CreateSentinelStore(
                    encryptionSettings,
                    loggerFactory);
                MasterKeySentinelResult validation = await sentinel.ValidateOrInitializeAsync(
                    encryptionSettings,
                    MasterKeySentinelInitializationMode.RequireCompatibilityEvidenceForExistingData,
                    context.RequestAborted);
                if (!validation.Success)
                {
                    return Results.BadRequest(new UnlockResponse(false, validation.Error ?? "Unlock failed."));
                }

                _ = CompleteUnlockAsync(
                    completion,
                    app,
                    new MasterKeyUnlockResult(encryptionSettings, submitted.MasterKey!));
                string message = validation.Repaired
                    ? "Master key sentinel repaired. Cotton is starting."
                    : validation.Created
                        ? "Master key initialized. Cotton is starting."
                        : "Master key accepted. Cotton is starting.";
                return Results.Ok(new UnlockResponse(true, message));
            });

            app.MapFallbackToFile("/index.html");

            using var stoppingRegistration = lifetime.ApplicationStopping.Register(
                () => completion.TrySetCanceled());

            await app.StartAsync();
            await LogUnlockAddressesAsync(app, logger, environment, bootstrapToken, firstUnlockExpiresAtUtc);

            try
            {
                return await completion.Task;
            }
            finally
            {
                await app.StopAsync();
                await app.DisposeAsync();
            }
        }

        private static bool IsLockedApiRequest(HttpRequest request) =>
            request.Path.StartsWithSegments(new PathString(Routes.V1.Base), StringComparison.OrdinalIgnoreCase);

        private static bool IsUnlockApiRequest(HttpRequest request)
        {
            PathString unlockApiPath = new(UnlockApiBase);
            PathString unlockStatusPath = new(UnlockApiBase + "/status");
            PathString unlockKeyPath = new(UnlockApiBase + "/key");

            return request.Path.Equals(unlockApiPath, StringComparison.OrdinalIgnoreCase)
                || request.Path.Equals(unlockStatusPath, StringComparison.OrdinalIgnoreCase)
                || request.Path.Equals(unlockKeyPath, StringComparison.OrdinalIgnoreCase);
        }

        private static async Task WriteLockedApiResponseAsync(HttpContext context)
        {
            DisableCaching(context);
            context.Response.StatusCode = StatusCodes.Status423Locked;
            await context.Response.WriteAsJsonAsync(
                new LockedApiResponse(true, "Cotton is locked until the master key is provided."),
                cancellationToken: context.RequestAborted);
        }

        private static void DisableCaching(HttpContext context)
        {
            context.Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers.Expires = "0";
        }

        private static async Task CompleteUnlockAsync(
            TaskCompletionSource<MasterKeyUnlockResult> completion,
            IHost host,
            MasterKeyUnlockResult result)
        {
            await Task.Delay(750);
            completion.TrySetResult(result);
            await host.StopAsync();
        }

        private static async Task<IResult?> ValidateBootstrapTokenAsync(
            IWebHostEnvironment environment,
            string? submittedBootstrapToken,
            string expectedBootstrapToken,
            DateTimeOffset firstUnlockExpiresAtUtc,
            CancellationToken cancellationToken)
        {
            if (!await RequiresBootstrapTokenAsync(environment, cancellationToken))
            {
                return null;
            }

            if (DateTimeOffset.UtcNow > firstUnlockExpiresAtUtc)
            {
                return Results.Json(
                    new UnlockResponse(false, "First unlock window expired. Restart Cotton and use the new bootstrap token from server logs."),
                    statusCode: StatusCodes.Status403Forbidden);
            }

            if (!IsBootstrapTokenValid(submittedBootstrapToken, expectedBootstrapToken))
            {
                return Results.Json(
                    new UnlockResponse(false, "Bootstrap token is required for the first unlock. Check the server logs."),
                    statusCode: StatusCodes.Status403Forbidden);
            }

            return null;
        }

        private static async Task<bool> RequiresBootstrapTokenAsync(
            IWebHostEnvironment environment,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return !environment.IsDevelopment()
                && !await MasterKeyStartupStorage.HasExistingCottonDataAsync(cancellationToken);
        }

        private static bool IsBootstrapTokenValid(string? submittedBootstrapToken, string expectedBootstrapToken)
        {
            if (string.IsNullOrWhiteSpace(submittedBootstrapToken))
            {
                return false;
            }

            byte[] submitted = Encoding.UTF8.GetBytes(submittedBootstrapToken.Trim());
            byte[] expected = Encoding.UTF8.GetBytes(expectedBootstrapToken);
            return submitted.Length == expected.Length
                && CryptographicOperations.FixedTimeEquals(submitted, expected);
        }

        private static async Task<SubmittedUnlockRequest> ReadSubmittedUnlockRequestAsync(HttpContext context)
        {
            if (context.Request.HasFormContentType)
            {
                IFormCollection form = await context.Request.ReadFormAsync();
                return new SubmittedUnlockRequest(
                    form["masterKey"].ToString().Trim(),
                    form["bootstrapToken"].ToString().Trim());
            }

            UnlockRequest? request = await JsonSerializer.DeserializeAsync<UnlockRequest>(
                context.Request.Body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                context.RequestAborted);
            return new SubmittedUnlockRequest(
                request?.MasterKey?.Trim(),
                request?.BootstrapToken?.Trim());
        }

        private static string GenerateRootMasterKey() =>
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));

        private static string GenerateBootstrapToken() =>
            Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

        private static async Task LogUnlockAddressesAsync(
            WebApplication app,
            ILogger logger,
            IWebHostEnvironment environment,
            string bootstrapToken,
            DateTimeOffset firstUnlockExpiresAtUtc)
        {
            string[] addresses = [.. app.Urls];
            bool requiresBootstrapToken = await RequiresBootstrapTokenAsync(environment);
            if (addresses.Length == 0)
            {
                if (requiresBootstrapToken)
                {
                    logger.LogWarning(
                        "COTTON_MASTER_KEY is not configured. First unlock bootstrap token is {BootstrapToken}; it expires at {ExpiresAtUtc:O}.",
                        bootstrapToken,
                        firstUnlockExpiresAtUtc);
                    return;
                }

                logger.LogWarning("COTTON_MASTER_KEY is not configured. Open /unlock to provide the master key.");
                return;
            }

            foreach (string address in addresses)
            {
                string unlockUrl = address.TrimEnd('/') + "/unlock";
                if (requiresBootstrapToken)
                {
                    logger.LogWarning(
                        "COTTON_MASTER_KEY is not configured. First unlock requires bootstrap token {BootstrapToken}; it expires at {ExpiresAtUtc:O}. Unlock Cotton at {UnlockUrl}",
                        bootstrapToken,
                        firstUnlockExpiresAtUtc,
                        unlockUrl);
                    continue;
                }

                logger.LogWarning(
                    "COTTON_MASTER_KEY is not configured. Unlock Cotton at {UnlockUrl}",
                    unlockUrl);
            }
        }

        private sealed record UnlockRequest(string? MasterKey, string? BootstrapToken);
        private sealed record SubmittedUnlockRequest(string? MasterKey, string? BootstrapToken);
        private sealed record UnlockStatusResponse(bool RequiresBootstrapToken, DateTimeOffset? FirstUnlockExpiresAtUtc);
        private sealed record UnlockResponse(bool Ok, string Message);
        private sealed record LockedApiResponse(bool Locked, string Message);
    }

    /// <summary>
    /// Master-key unlock result containing the derived legacy settings and the unlock secret used for keyring slots.
    /// </summary>
    public sealed record MasterKeyUnlockResult(CottonEncryptionSettings Settings, string UnlockSecret);

}
