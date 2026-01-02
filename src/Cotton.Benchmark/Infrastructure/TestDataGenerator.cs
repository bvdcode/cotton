// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.Security.Cryptography;
using System.Text;

namespace Cotton.Benchmark.Infrastructure
{
    /// <summary>
    /// Generates realistic test data for benchmarks.
    /// </summary>
    public static class TestDataGenerator
    {
        /// <summary>
        /// Generates compressible text data (like logs or documents).
        /// </summary>
        public static byte[] GenerateCompressibleText(int sizeBytes)
        {
            var sb = new StringBuilder();
            var random = new Random(42); // Fixed seed for reproducibility

            string[] patterns =
            [
                "INFO: Processing request from user {0} at {1}\n",
                "DEBUG: Database query executed successfully, returned {0} rows\n",
                "WARN: High memory usage detected: {0} MB\n",
                "ERROR: Failed to connect to service {0}, retrying...\n",
                "TRACE: Method {0} executed in {1} ms\n"
            ];

            while (sb.Length < sizeBytes)
            {
                var pattern = patterns[random.Next(patterns.Length)];
                var message = string.Format(pattern,
                    random.Next(1000),
                    DateTime.Now.AddSeconds(-random.Next(3600)));
                sb.Append(message);
            }

            var text = sb.ToString();
            return Encoding.UTF8.GetBytes(text[..Math.Min(text.Length, sizeBytes)]);
        }

        /// <summary>
        /// Generates random binary data (incompressible).
        /// </summary>
        public static byte[] GenerateRandomBinary(int sizeBytes)
        {
            var data = new byte[sizeBytes];
            RandomNumberGenerator.Fill(data);
            return data;
        }

        /// <summary>
        /// Generates semi-compressible data (realistic file content).
        /// </summary>
        public static byte[] GenerateMixedData(int sizeBytes)
        {
            var data = new byte[sizeBytes];
            var random = new Random(42);

            // Fill with patterns that have some repetition
            for (int i = 0; i < sizeBytes; i++)
            {
                if (i % 100 < 50)
                {
                    // Repeated pattern
                    data[i] = (byte)(i % 10);
                }
                else
                {
                    // Random data
                    data[i] = (byte)random.Next(256);
                }
            }

            return data;
        }

        /// <summary>
        /// Generates data that simulates JSON content.
        /// </summary>
        public static byte[] GenerateJsonData(int sizeBytes)
        {
            var sb = new StringBuilder();
            var random = new Random(42);

            sb.Append("[\n");

            while (sb.Length < sizeBytes - 100)
            {
                sb.Append("  {\n");
                sb.Append($"    \"id\": {random.Next(1000000)},\n");
                sb.Append($"    \"name\": \"User {random.Next(10000)}\",\n");
                sb.Append($"    \"email\": \"user{random.Next(10000)}@example.com\",\n");
                sb.Append($"    \"timestamp\": \"{DateTime.Now.AddDays(-random.Next(365)):O}\",\n");
                sb.Append($"    \"active\": {(random.Next(2) == 0 ? "true" : "false")}\n");
                sb.Append("  },\n");
            }

            sb.Append("]\n");

            var text = sb.ToString();
            return Encoding.UTF8.GetBytes(text[..Math.Min(text.Length, sizeBytes)]);
        }
    }
}
