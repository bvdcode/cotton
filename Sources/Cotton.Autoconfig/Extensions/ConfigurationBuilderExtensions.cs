using EasyExtensions.Helpers;
using Microsoft.Extensions.Configuration;

namespace Cotton.Autoconfig.Extensions
{
    public static class ConfigurationBuilderExtensions
    {
        public static IConfigurationBuilder AddCottonOptions(this IConfigurationBuilder configurationBuilder)
        {
            string postgresHost = Environment.GetEnvironmentVariable("COTTON_PG_HOST") ?? "localhost";
            string postgresPortStr = Environment.GetEnvironmentVariable("COTTON_PG_PORT") ?? "5432";
            string postgresDb = Environment.GetEnvironmentVariable("COTTON_PG_DATABASE") ?? "cotton_dev";
            string postgresUser = Environment.GetEnvironmentVariable("COTTON_PG_USERNAME") ?? "postgres";
            string postgresPass = Environment.GetEnvironmentVariable("COTTON_PG_PASSWORD") ?? "postgres";
            ushort postgresPort = ushort.Parse(postgresPortStr);

            string jwtKey = StringHelpers.CreateRandomString(128);

            const int masterKeyId = 1;
            string masterEncryptionKey = Environment.GetEnvironmentVariable("COTTON_MASTER_KEY") ?? "devedovolovopeperepolevopopovedo";
            string pepper = masterEncryptionKey;

            const int defaultEncryptionThreads = 4;
            const int defaultMaxChunkSizeBytes = 64 * 1024 * 1024;
            const int defaultCipherChunkSizeBytes = 1 * 1024 * 1024;


            var dict = new Dictionary<string, string?>
            {
                // DB
                ["DatabaseSettings:Host"] = postgresHost,
                ["DatabaseSettings:Port"] = postgresPort.ToString(),
                ["DatabaseSettings:Database"] = postgresDb,
                ["DatabaseSettings:Username"] = postgresUser,
                ["DatabaseSettings:Password"] = postgresPass,

                // Crypto / Pepper
                ["MasterKeyId"] = masterKeyId.ToString(),
                ["MasterKey"] = masterEncryptionKey,
                ["Pepper"] = pepper,
                ["EncryptionThreads"] = defaultEncryptionThreads.ToString(),
                ["MaxChunkSizeBytes"] = defaultMaxChunkSizeBytes.ToString(),
                ["CipherChunkSizeBytes"] = defaultCipherChunkSizeBytes.ToString(),

                // JWT
                ["JwtSettings:Key"] = jwtKey,
            };

            return configurationBuilder.AddInMemoryCollection(dict);
        }
    }
}
