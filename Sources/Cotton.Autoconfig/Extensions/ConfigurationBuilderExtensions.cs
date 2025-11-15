using Cotton.Crypto;
using Cotton.Shared;
using EasyExtensions.Helpers;
using Microsoft.Extensions.Configuration;

namespace Cotton.Autoconfig.Extensions
{
    public static class ConfigurationBuilderExtensions
    {
        public const int MasterKeyLength = 32;

        public static IConfigurationBuilder AddCottonOptions(this IConfigurationBuilder configurationBuilder)
        {
            string postgresHost = Environment.GetEnvironmentVariable("COTTON_PG_HOST") ?? "localhost";
            string postgresPortStr = Environment.GetEnvironmentVariable("COTTON_PG_PORT") ?? "5432";
            string postgresDb = Environment.GetEnvironmentVariable("COTTON_PG_DATABASE") ?? "cotton_dev";
            string postgresUser = Environment.GetEnvironmentVariable("COTTON_PG_USERNAME") ?? "postgres";
            string postgresPass = Environment.GetEnvironmentVariable("COTTON_PG_PASSWORD") ?? "postgres";
            Environment.SetEnvironmentVariable("COTTON_PG_PASSWORD", StringHelpers.CreatePseudoRandomString(32));
            ushort postgresPort = ushort.Parse(postgresPortStr);

            string jwtKey = StringHelpers.CreateRandomString(64);
            const int masterKeyId = 1;
            string rootMasterEncryptionKey = Environment.GetEnvironmentVariable("COTTON_MASTER_KEY") ?? "devedovolovopeperepolevopopovedo";
            if (rootMasterEncryptionKey.Length != MasterKeyLength)
            {
                throw new InvalidOperationException($"COTTON_MASTER_KEY must be set and be exactly {MasterKeyLength} characters long.");
            }
            Environment.SetEnvironmentVariable("COTTON_MASTER_KEY", StringHelpers.CreatePseudoRandomString(MasterKeyLength));

            string pepper = KeyDerivation.DeriveSubkeyBase64(rootMasterEncryptionKey, "CottonPepper", MasterKeyLength);
            string masterEncryptionKey = KeyDerivation.DeriveSubkeyBase64(rootMasterEncryptionKey, "CottonMasterEncryptionKey", MasterKeyLength);

            const int defaultEncryptionThreads = 4;
            const int defaultMaxChunkSizeBytes = 64 * 1024 * 1024;
            const int defaultCipherChunkSizeBytes = 1 * 1024 * 1024;

            var dict = new Dictionary<string, string?>
            {
                ["JwtSettings:Key"] = jwtKey,
                ["DatabaseSettings:Host"] = postgresHost,
                ["DatabaseSettings:Port"] = postgresPort.ToString(),
                ["DatabaseSettings:Database"] = postgresDb,
                ["DatabaseSettings:Username"] = postgresUser,
                ["DatabaseSettings:Password"] = postgresPass,

                [nameof(CottonSettings.MasterEncryptionKeyId)] = masterKeyId.ToString(),
                [nameof(CottonSettings.MasterEncryptionKey)] = masterEncryptionKey,
                [nameof(CottonSettings.Pepper)] = pepper,
                [nameof(CottonSettings.EncryptionThreads)] = defaultEncryptionThreads.ToString(),
                [nameof(CottonSettings.MaxChunkSizeBytes)] = defaultMaxChunkSizeBytes.ToString(),
                [nameof(CottonSettings.CipherChunkSizeBytes)] = defaultCipherChunkSizeBytes.ToString(),
            };

            return configurationBuilder.AddInMemoryCollection(dict);
        }
    }
}
