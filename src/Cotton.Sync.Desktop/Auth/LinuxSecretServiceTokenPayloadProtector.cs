// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Cotton.Sync.Desktop.Auth
{
    internal class LinuxSecretServiceTokenPayloadProtector : ITokenPayloadProtector, IDeletableTokenPayloadProtector
    {
        private const string ApplicationAttribute = "application";
        private const string ApplicationValue = "cotton-sync-desktop";
        private const string IdAttribute = "id";
        private const string Label = "Cotton Sync Desktop tokens";
        private const string PurposeAttribute = "purpose";
        private const string PurposeValue = "token-payload";
        private const string ServiceAttribute = "service";
        private const string ServiceValue = "cotton-sync";

        private readonly string _secretToolPath;
        private readonly ISecretToolProcessRunner _runner;

        public LinuxSecretServiceTokenPayloadProtector(string secretToolPath)
            : this(secretToolPath, new SecretToolProcessRunner())
        {
        }

        internal LinuxSecretServiceTokenPayloadProtector(string secretToolPath, ISecretToolProcessRunner runner)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(secretToolPath);
            _secretToolPath = secretToolPath.Trim();
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        }

        public string Scheme => "linux-secret-service-v1";

        public async Task<byte[]> ProtectAsync(byte[] plaintext, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(plaintext);
            cancellationToken.ThrowIfCancellationRequested();
            string id = Guid.NewGuid().ToString("N");
            string secret = Convert.ToBase64String(plaintext);
            await _runner
                .RunAsync(CreateStoreStartInfo(_secretToolPath, id), secret, cancellationToken)
                .ConfigureAwait(false);
            return Encoding.UTF8.GetBytes(id);
        }

        public async Task<byte[]> UnprotectAsync(byte[] protectedPayload, CancellationToken cancellationToken = default)
        {
            string id = DecodeId(protectedPayload);
            string secret = await _runner
                .ReadAsync(CreateLookupStartInfo(_secretToolPath, id), cancellationToken)
                .ConfigureAwait(false);
            try
            {
                return Convert.FromBase64String(secret.TrimEnd('\r', '\n'));
            }
            catch (FormatException exception)
            {
                throw new CryptographicException("Secret Service returned an invalid token payload.", exception);
            }
        }

        public Task DeleteAsync(byte[] protectedPayload, CancellationToken cancellationToken = default)
        {
            string id = DecodeId(protectedPayload);
            return _runner.RunAsync(CreateClearStartInfo(_secretToolPath, id), null, cancellationToken);
        }

        internal static ProcessStartInfo CreateStoreStartInfo(string secretToolPath, string id)
        {
            ProcessStartInfo startInfo = CreateBaseStartInfo(secretToolPath);
            startInfo.ArgumentList.Add("store");
            startInfo.ArgumentList.Add("--label");
            startInfo.ArgumentList.Add(Label);
            AddAttributes(startInfo, id);
            return startInfo;
        }

        internal static ProcessStartInfo CreateLookupStartInfo(string secretToolPath, string id)
        {
            ProcessStartInfo startInfo = CreateBaseStartInfo(secretToolPath);
            startInfo.ArgumentList.Add("lookup");
            AddAttributes(startInfo, id);
            return startInfo;
        }

        internal static ProcessStartInfo CreateClearStartInfo(string secretToolPath, string id)
        {
            ProcessStartInfo startInfo = CreateBaseStartInfo(secretToolPath);
            startInfo.ArgumentList.Add("clear");
            AddAttributes(startInfo, id);
            return startInfo;
        }

        private static ProcessStartInfo CreateBaseStartInfo(string secretToolPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(secretToolPath);
            return new ProcessStartInfo
            {
                FileName = secretToolPath,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
        }

        private static void AddAttributes(ProcessStartInfo startInfo, string id)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            startInfo.ArgumentList.Add(ServiceAttribute);
            startInfo.ArgumentList.Add(ServiceValue);
            startInfo.ArgumentList.Add(ApplicationAttribute);
            startInfo.ArgumentList.Add(ApplicationValue);
            startInfo.ArgumentList.Add(PurposeAttribute);
            startInfo.ArgumentList.Add(PurposeValue);
            startInfo.ArgumentList.Add(IdAttribute);
            startInfo.ArgumentList.Add(id);
        }

        private static string DecodeId(byte[] protectedPayload)
        {
            ArgumentNullException.ThrowIfNull(protectedPayload);
            string id = Encoding.UTF8.GetString(protectedPayload).Trim();
            if (id.Length == 0)
            {
                throw new CryptographicException("Secret Service token payload id is empty.");
            }

            return id;
        }
    }
}
