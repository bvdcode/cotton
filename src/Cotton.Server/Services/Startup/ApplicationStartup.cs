// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Autoconfig.Extensions;
using Cotton.Server.Extensions;

namespace Cotton.Server.Services.Startup
{
    internal static class ApplicationStartup
    {
        public static async Task RunAsync(string[] args)
        {
            ConfigureProcessTimeZone();
            ProcessHardeningStatus processHardeningStatus = LinuxProcessHardening.ApplyFromEnvironment();

            CottonEncryptionSettings encryptionSettings;
            MasterKeyRuntimeState masterKeyRuntimeState;
            try
            {
                (encryptionSettings, masterKeyRuntimeState) = await ResolveEncryptionSettingsAsync(args);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await RunApplicationAsync(args, encryptionSettings, masterKeyRuntimeState, processHardeningStatus);
        }

        private static void ConfigureProcessTimeZone()
        {
            Environment.SetEnvironmentVariable("TZ", "UTC");
            TimeZoneInfo.ClearCachedData();
        }

        private static async Task<(CottonEncryptionSettings Settings, MasterKeyRuntimeState RuntimeState)> ResolveEncryptionSettingsAsync(string[] args)
        {
            string? rootMasterKey = Environment.GetEnvironmentVariable(
                ConfigurationBuilderExtensions.MasterKeyEnvironmentVariable);
            if (string.IsNullOrEmpty(rootMasterKey))
            {
                ConfigurationBuilderExtensions.ClearMasterKeyEnvironmentVariable();
                CottonEncryptionSettings settings = await MasterKeyUnlockServer.WaitForUnlockAsync(args);
                return (settings, MasterKeyRuntimeState.FromUnlock(IsMasterKeyEnvironmentVariablePresent()));
            }

            try
            {
                return (
                    ConfigurationBuilderExtensions.DeriveEncryptionSettings(rootMasterKey),
                    MasterKeyRuntimeState.FromEnvironment(environmentVariablePresentAfterResolution: false));
            }
            finally
            {
                ConfigurationBuilderExtensions.ClearMasterKeyEnvironmentVariable();
            }
        }

        private static bool IsMasterKeyEnvironmentVariablePresent()
        {
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(
                ConfigurationBuilderExtensions.MasterKeyEnvironmentVariable));
        }

        private static async Task RunApplicationAsync(
            string[] args,
            CottonEncryptionSettings encryptionSettings,
            MasterKeyRuntimeState masterKeyRuntimeState,
            ProcessHardeningStatus processHardeningStatus)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            builder.AddCottonServer(encryptionSettings, masterKeyRuntimeState, processHardeningStatus);
            WebApplication app = builder.Build();
            StartupBlocker? blocker;
            using (IServiceScope scope = app.Services.CreateScope())
            {
                StartupPreflightValidator validator = scope.ServiceProvider.GetRequiredService<StartupPreflightValidator>();
                blocker = await validator.ValidateAsync(CancellationToken.None);
            }

            if (blocker is not null)
            {
                await app.DisposeAsync();
                await StartupBlockedServer.RunAsync(args, blocker);
                return;
            }

            app.UseCottonServer();
            await DatabaseStartup.InitializeAsync(app.Services, app.Lifetime.ApplicationStopping);
            await app.RunAsync();
        }
    }
}
