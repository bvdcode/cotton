using Cotton.Shared;

namespace Cotton.Autoconfig
{
    internal static class ConfigurationProvider
    {
        internal static CottonSettings Settings => GetSettings();

        private static CottonSettings GetSettings()
        {
            /*
             * 
              "DatabaseSettings": {
                "Host": "localhost",
                "Port": 5432,
                "Database": "cotton_dev",
                "Username": "postgres",
                "Password": "postgres"
              },
              "Pepper": "2JfKvseABmt5lrY8a5UkKZ5RfzXq7oo3",
              "MasterEncryptionKey": "MyDevelopmentEncryptionKey123!",
              "MasterEncryptionKeyId": 1,
              "EncryptionThreads": 4,
              "MaxChunkSizeBytes": 16777216,
              "CipherChunkSizeBytes": 20971520,
              "JwtSettings": {
                "Key": "T3wNTuKqmTXKjJKXHJRGUpG9sdrmpSX4"
              }
            */

            return new CottonSettings
            {
                Pepper = "GVGguoWEOfxjqUQ5HewzMXeqjkHGALxc",
                MasterEncryptionKey = "VSRyHuJXezjfV4OXgP70ZUItdkzolm9K",
                MaxChunkSizeBytes = 10 * 1024 * 1024, // 10 MB
                MasterEncryptionKeyId = 1,
                EncryptionThreads = Environment.ProcessorCount,
                CipherChunkSizeBytes = 64 * 1024, // 64 KB
                //DatabaseSettings = new DatabaseSettings
                //{
                //    Host = "localhost",
                //    Port = 5432,
                //    DatabaseName = "cotton_db",
                //    Username = "cotton_user",
                //    Password = "secure_password"
                //}
            };
        }
    }
}
