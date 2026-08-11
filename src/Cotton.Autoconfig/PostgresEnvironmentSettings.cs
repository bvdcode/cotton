// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Globalization;

namespace Cotton.Autoconfig
{
    /// <summary>
    /// Represents PostgreSQL connection settings read from the Cotton environment contract.
    /// </summary>
    public record PostgresEnvironmentSettings(
        string Host,
        ushort Port,
        string Database,
        string Username,
        string Password)
    {
        internal const string HostEnvironmentVariable = "COTTON_PG_HOST";
        internal const string PortEnvironmentVariable = "COTTON_PG_PORT";
        internal const string DatabaseEnvironmentVariable = "COTTON_PG_DATABASE";
        internal const string UsernameEnvironmentVariable = "COTTON_PG_USERNAME";
        internal const string PasswordEnvironmentVariable = "COTTON_PG_PASSWORD";

        internal const string DefaultHost = "localhost";
        internal const ushort DefaultPort = 5432;
        internal const string DefaultDatabase = "cotton_dev";
        internal const string DefaultUsername = "postgres";
        internal const string DefaultPassword = "postgres";

        /// <summary>
        /// Reads PostgreSQL settings from the current process environment.
        /// </summary>
        public static PostgresEnvironmentSettings FromEnvironment()
        {
            string portValue = GetEnvironmentValue(
                PortEnvironmentVariable,
                DefaultPort.ToString(CultureInfo.InvariantCulture));
            if (!ushort.TryParse(
                portValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out ushort port))
            {
                throw new InvalidOperationException(
                    $"{PortEnvironmentVariable} must be a valid unsigned 16-bit integer.");
            }

            return new PostgresEnvironmentSettings(
                GetEnvironmentValue(HostEnvironmentVariable, DefaultHost),
                port,
                GetEnvironmentValue(DatabaseEnvironmentVariable, DefaultDatabase),
                GetEnvironmentValue(UsernameEnvironmentVariable, DefaultUsername),
                GetEnvironmentValue(PasswordEnvironmentVariable, DefaultPassword));
        }

        private static string GetEnvironmentValue(string name, string defaultValue)
        {
            return Environment.GetEnvironmentVariable(name) ?? defaultValue;
        }
    }
}
