// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using EasyExtensions.Helpers;
using Microsoft.Extensions.Configuration;

namespace Cotton.Autoconfig.Extensions
{
    /// <summary>
    /// Adds Cotton runtime configuration derived from environment variables and the root master key.
    /// </summary>
    public static class ConfigurationBuilderExtensions
    {
        /// <summary>
        /// Name of the environment variable containing the root master key.
        /// </summary>
        public const string MasterKeyEnvironmentVariable = "COTTON_MASTER_KEY";

        /// <summary>
        /// IMPORTANT: Length of the master key in characters.
        /// DO NOT CHANGE THIS VALUE once it is set for a deployment,
        /// as it will invalidate all existing data encrypted with derived keys
        /// and make it unrecoverable, including user passwords.
        /// </summary>
        public const int DefaultKeyLength = 32;

        /// <summary>
        /// Identifier assigned to the initial master-key derivation scheme.
        /// </summary>
        public const int DefaultMasterKeyId = 1;

        /// <summary>
        /// Adds Cotton options by reading the root master key from the environment.
        /// </summary>
        public static IConfigurationBuilder AddCottonOptions(this IConfigurationBuilder configurationBuilder)
        {
            string rootMasterEncryptionKey = Environment.GetEnvironmentVariable(MasterKeyEnvironmentVariable)
                ?? throw new InvalidOperationException(
                    $"{MasterKeyEnvironmentVariable} must be set and be exactly {DefaultKeyLength} characters long.");
            try
            {
                return configurationBuilder.AddCottonOptions(rootMasterEncryptionKey);
            }
            finally
            {
                ClearMasterKeyEnvironmentVariable();
            }
        }

        /// <summary>
        /// Adds Cotton options by deriving encryption settings from the supplied root master key.
        /// </summary>
        public static IConfigurationBuilder AddCottonOptions(
            this IConfigurationBuilder configurationBuilder,
            string rootMasterEncryptionKey)
        {
            CottonEncryptionSettings encryptionSettings = DeriveEncryptionSettings(rootMasterEncryptionKey);
            return configurationBuilder.AddCottonOptions(encryptionSettings);
        }

        /// <summary>
        /// Adds Cotton options using already-derived encryption settings.
        /// </summary>
        public static IConfigurationBuilder AddCottonOptions(
            this IConfigurationBuilder configurationBuilder,
            CottonEncryptionSettings encryptionSettings)
        {
            string postgresHost = Environment.GetEnvironmentVariable("COTTON_PG_HOST") ?? "localhost";
            string postgresPortStr = Environment.GetEnvironmentVariable("COTTON_PG_PORT") ?? "5432";
            string postgresDb = Environment.GetEnvironmentVariable("COTTON_PG_DATABASE") ?? "cotton_dev";
            string postgresUser = Environment.GetEnvironmentVariable("COTTON_PG_USERNAME") ?? "postgres";
            string postgresPass = Environment.GetEnvironmentVariable("COTTON_PG_PASSWORD") ?? "postgres";
            ushort postgresPort = ushort.Parse(postgresPortStr);
            Environment.SetEnvironmentVariable("COTTON_PG_PASSWORD", null, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("COTTON_PG_PASSWORD", null, EnvironmentVariableTarget.User);

            string jwtKey = StringHelpers.CreateRandomString(DefaultKeyLength);

            var dict = new Dictionary<string, string?>
            {
                ["JwtSettings:Key"] = jwtKey,
                ["DatabaseSettings:Host"] = postgresHost,
                ["DatabaseSettings:Port"] = postgresPort.ToString(),
                ["DatabaseSettings:Database"] = postgresDb,
                ["DatabaseSettings:Username"] = postgresUser,
                ["DatabaseSettings:Password"] = postgresPass,

                [nameof(CottonEncryptionSettings.Pepper)] = encryptionSettings.Pepper,
                [nameof(CottonEncryptionSettings.MasterEncryptionKey)] = encryptionSettings.MasterEncryptionKey,
                [nameof(CottonEncryptionSettings.MasterEncryptionKeyId)] = encryptionSettings.MasterEncryptionKeyId.ToString(),
            };

            return configurationBuilder.AddInMemoryCollection(dict);
        }

        /// <summary>
        /// Derives all process-local Cotton encryption settings from the root master key.
        /// </summary>
        public static CottonEncryptionSettings DeriveEncryptionSettings(string rootMasterEncryptionKey)
        {
            ValidateRootMasterKey(rootMasterEncryptionKey);

            return new CottonEncryptionSettings
            {
                Pepper = KeyDerivation.DeriveSubkeyBase64(rootMasterEncryptionKey, "CottonPepper", DefaultKeyLength),
                MasterEncryptionKey = KeyDerivation.DeriveSubkeyBase64(rootMasterEncryptionKey, "CottonMasterEncryptionKey", DefaultKeyLength),
                MasterEncryptionKeyId = DefaultMasterKeyId,
            };
        }

        /// <summary>
        /// Validates that the configured root master key matches Cotton's fixed key-length contract.
        /// </summary>
        public static void ValidateRootMasterKey(string? rootMasterEncryptionKey)
        {
            if (rootMasterEncryptionKey is null)
            {
                throw new InvalidOperationException(
                    $"{MasterKeyEnvironmentVariable} must be set and be exactly {DefaultKeyLength} characters long.");
            }

            if (rootMasterEncryptionKey.Length != DefaultKeyLength)
            {
                throw new InvalidOperationException(
                    $"{MasterKeyEnvironmentVariable} must be exactly {DefaultKeyLength} characters long.");
            }
        }

        /// <summary>
        /// Clears the root master key from process and user environment variables after startup.
        /// </summary>
        public static void ClearMasterKeyEnvironmentVariable()
        {
            Environment.SetEnvironmentVariable(MasterKeyEnvironmentVariable, null, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(MasterKeyEnvironmentVariable, null, EnvironmentVariableTarget.User);
        }
    }
}
