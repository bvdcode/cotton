// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sdk;
using Cotton.Sync.State;

namespace Cotton.Sync.Cli
{

    internal static class SyncCliOptionsReader
    {
        public static SyncCliConnectionOptions? ReadConnectionOptions(
            IReadOnlyList<string> args,
            TextWriter error,
            string command,
            bool allowBrowserLogin = false)
        {
            bool useBrowserLogin = allowBrowserLogin && HasFlag(args, "--browser-login");
            string? server = ReadOption(args, "--server");
            string? username = ReadOption(args, "--username");
            string? password = ReadPassword(args);
            string? localRoot = ReadOption(args, "--local-root");
            string? remoteRoot = ReadOption(args, "--remote-root");
            string? syncPairId = ReadOption(args, "--sync-pair");
            string? databasePath = ReadOption(args, "--database");
            string? twoFactorCode = ReadOption(args, "--two-factor-code");
            if (string.IsNullOrWhiteSpace(server)
                || string.IsNullOrWhiteSpace(localRoot)
                || string.IsNullOrWhiteSpace(remoteRoot)
                || string.IsNullOrWhiteSpace(syncPairId)
                || string.IsNullOrWhiteSpace(databasePath))
            {
                error.WriteLine(
                    command + " requires --server, --local-root, --remote-root, --sync-pair, and --database.");
                return null;
            }

            if (useBrowserLogin)
            {
                if (!string.IsNullOrWhiteSpace(username)
                    || !string.IsNullOrWhiteSpace(password)
                    || !string.IsNullOrWhiteSpace(twoFactorCode))
                {
                    error.WriteLine("--browser-login cannot be combined with password sign-in options.");
                    return null;
                }
            }
            else if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                error.WriteLine(
                    command + " requires --username and --password or --password-env unless --browser-login is used.");
                return null;
            }

            Uri? serverUri = CottonServerUrl.NormalizeOptional(server);
            if (serverUri is null)
            {
                error.WriteLine("--server must be an HTTP or HTTPS URL.");
                return null;
            }

            if (!Guid.TryParse(remoteRoot, out Guid remoteRootNodeId))
            {
                error.WriteLine("--remote-root must be a node id GUID.");
                return null;
            }

            return new SyncCliConnectionOptions(
                serverUri,
                useBrowserLogin ? null : username!.Trim(),
                useBrowserLogin ? null : password!,
                localRoot,
                remoteRootNodeId,
                syncPairId.Trim(),
                databasePath,
                string.IsNullOrWhiteSpace(twoFactorCode) ? null : twoFactorCode.Trim(),
                useBrowserLogin);
        }

        public static SyncCliBrowserAuthOptions? ReadBrowserAuthOptions(
            IReadOnlyList<string> args,
            TextWriter error)
        {
            string? server = ReadOption(args, "--server");
            if (string.IsNullOrWhiteSpace(server))
            {
                error.WriteLine("auth-browser requires --server.");
                return null;
            }

            Uri? serverUri = CottonServerUrl.NormalizeOptional(server);
            if (serverUri is null)
            {
                error.WriteLine("--server must be an HTTP or HTTPS URL.");
                return null;
            }

            string applicationName = ReadOption(args, "--application-name")?.Trim() ?? "Cotton Sync CLI";
            if (string.IsNullOrWhiteSpace(applicationName))
            {
                error.WriteLine("--application-name must not be empty.");
                return null;
            }

            return new SyncCliBrowserAuthOptions(
                serverUri,
                applicationName,
                NormalizeOptional(ReadOption(args, "--application-version")),
                NormalizeOptional(ReadOption(args, "--device-name")) ?? "Cotton Sync CLI");
        }

        public static bool TryReadOptionalPositiveInt(
            IReadOnlyList<string> args,
            string name,
            TextWriter error,
            out int? value)
        {
            value = null;
            string? rawValue = ReadOption(args, name);
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return true;
            }

            if (!int.TryParse(
                    rawValue.Trim(),
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out int parsedValue)
                || parsedValue <= 0)
            {
                error.WriteLine(name + " must be a positive integer.");
                return false;
            }

            value = parsedValue;
            return true;
        }

        public static bool TryNormalizeProbeFile(
            string localRoot,
            string probeFile,
            out string normalizedProbeFile,
            out string error)
        {
            normalizedProbeFile = string.Empty;
            error = string.Empty;
            if (Path.IsPathRooted(probeFile))
            {
                error = "--probe-file must be a relative path inside --local-root.";
                return false;
            }

            try
            {
                normalizedProbeFile = SyncPath.Normalize(probeFile);
            }
            catch (ArgumentException exception)
            {
                error = "--probe-file is invalid: " + exception.Message;
                return false;
            }

            string root = Path.GetFullPath(localRoot);
            string fullPath = Path.GetFullPath(Path.Combine(root, normalizedProbeFile.Replace('/', Path.DirectorySeparatorChar)));
            string rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
                ? root
                : root + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal)
                && !string.Equals(fullPath, root, StringComparison.Ordinal))
            {
                error = "--probe-file must stay inside --local-root.";
                return false;
            }

            return true;
        }

        public static string? ReadOption(IReadOnlyList<string> args, string name)
        {
            for (int index = 0; index < args.Count - 1; index++)
            {
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[index + 1];
                }
            }

            return null;
        }

        public static bool HasFlag(IReadOnlyList<string> args, string name)
        {
            return args.Any(argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));
        }

        private static string? ReadPassword(IReadOnlyList<string> args)
        {
            string? password = ReadOption(args, "--password");
            if (!string.IsNullOrWhiteSpace(password))
            {
                return password;
            }

            string? passwordEnvironmentVariable = ReadOption(args, "--password-env");
            if (string.IsNullOrWhiteSpace(passwordEnvironmentVariable))
            {
                return null;
            }

            return Environment.GetEnvironmentVariable(passwordEnvironmentVariable.Trim());
        }

        private static string? NormalizeOptional(string? value)
        {
            string? trimmed = value?.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }
    }
}
