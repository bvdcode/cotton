// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Cotton.Auth;
using Cotton.Sdk.Auth;

namespace Cotton.Sync.Desktop.Auth
{
    internal class FileCottonTokenStore : ICottonTokenStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = false,
        };

        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly string _path;
        private readonly ITokenPayloadProtector _protector;

        public FileCottonTokenStore(string path)
            : this(path, DesktopTokenPayloadProtectorFactory.CreateDefault())
        {
        }

        internal FileCottonTokenStore(string path, ITokenPayloadProtector protector)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            _path = path;
            _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        }

        public async Task<TokenPairDto?> GetAsync(CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!File.Exists(_path))
                {
                    return null;
                }

                try
                {
                    await using FileStream stream = File.OpenRead(_path);
                    StoredTokenEnvelope? envelope = await JsonSerializer
                        .DeserializeAsync<StoredTokenEnvelope>(stream, JsonOptions, cancellationToken)
                        .ConfigureAwait(false);
                    TokenPairDto? tokens = await ReadTokensAsync(envelope, cancellationToken).ConfigureAwait(false);
                    return tokens is not null && IsUsable(tokens) ? Clone(tokens) : null;
                }
                catch (Exception exception) when (IsUnreadableTokenFileException(exception))
                {
                    Trace.TraceWarning("Stored Cotton token file is unreadable and will be ignored: {0}", exception);
                    return null;
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task SaveAsync(TokenPairDto tokens, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(tokens);
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                EnsureDirectoryExists();
                StoredTokenEnvelope? previousEnvelope = await ReadEnvelopeAsync(cancellationToken).ConfigureAwait(false);
                string tempPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                StoredTokenEnvelope? createdEnvelope = null;
                bool committed = false;
                try
                {
                    createdEnvelope = await CreateEnvelopeAsync(tokens, cancellationToken).ConfigureAwait(false);
                    await using (FileStream stream = File.Create(tempPath))
                    {
                        await JsonSerializer.SerializeAsync(stream, createdEnvelope, JsonOptions, cancellationToken)
                            .ConfigureAwait(false);
                        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }

                    RestrictFileAccess(tempPath);
                    File.Move(tempPath, _path, overwrite: true);
                    RestrictFileAccess(_path);
                    committed = true;
                    await DeleteProtectedPayloadAsync(previousEnvelope, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    if (!committed)
                    {
                        await DeleteProtectedPayloadAsync(createdEnvelope, CancellationToken.None).ConfigureAwait(false);
                    }

                    DeleteIfExists(tempPath);
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                StoredTokenEnvelope? envelope = await ReadEnvelopeAsync(cancellationToken).ConfigureAwait(false);
                DeleteIfExists(_path);
                await DeleteProtectedPayloadAsync(envelope, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        private static bool IsUsable(TokenPairDto tokens)
        {
            return !string.IsNullOrWhiteSpace(tokens.AccessToken)
                && !string.IsNullOrWhiteSpace(tokens.RefreshToken);
        }

        private static bool IsUnreadableTokenFileException(Exception exception)
        {
            return exception is JsonException
                or FormatException
                or CryptographicException
                or PlatformNotSupportedException;
        }

        private async Task<StoredTokenEnvelope> CreateEnvelopeAsync(
            TokenPairDto tokens,
            CancellationToken cancellationToken)
        {
            byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(Clone(tokens), JsonOptions);
            byte[] protectedPayload = await _protector
                .ProtectAsync(plaintext, cancellationToken)
                .ConfigureAwait(false);
            return new StoredTokenEnvelope
            {
                Scheme = _protector.Scheme,
                Payload = Convert.ToBase64String(protectedPayload),
            };
        }

        private async Task<TokenPairDto?> ReadTokensAsync(
            StoredTokenEnvelope? envelope,
            CancellationToken cancellationToken)
        {
            if (envelope is null
                || !string.Equals(envelope.Scheme, _protector.Scheme, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(envelope.Payload))
            {
                return null;
            }

            byte[] protectedPayload;
            try
            {
                protectedPayload = Convert.FromBase64String(envelope.Payload);
            }
            catch (FormatException)
            {
                return null;
            }

            byte[] plaintext = await _protector
                .UnprotectAsync(protectedPayload, cancellationToken)
                .ConfigureAwait(false);
            return JsonSerializer.Deserialize<TokenPairDto>(plaintext, JsonOptions);
        }

        private async Task<StoredTokenEnvelope?> ReadEnvelopeAsync(CancellationToken cancellationToken)
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            try
            {
                await using FileStream stream = File.OpenRead(_path);
                return await JsonSerializer
                    .DeserializeAsync<StoredTokenEnvelope>(stream, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (IsUnreadableTokenFileException(exception))
            {
                Trace.TraceWarning("Stored Cotton token envelope is unreadable and cannot be cleaned up: {0}", exception);
                return null;
            }
        }

        private async Task DeleteProtectedPayloadAsync(
            StoredTokenEnvelope? envelope,
            CancellationToken cancellationToken)
        {
            if (_protector is not IDeletableTokenPayloadProtector deletable
                || envelope is null
                || !string.Equals(envelope.Scheme, _protector.Scheme, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(envelope.Payload))
            {
                return;
            }

            byte[] protectedPayload;
            try
            {
                protectedPayload = Convert.FromBase64String(envelope.Payload);
            }
            catch (FormatException exception)
            {
                Trace.TraceWarning("Stored Cotton token envelope has an invalid protected payload: {0}", exception);
                return;
            }

            try
            {
                await deletable.DeleteAsync(protectedPayload, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsExternalSecretCleanupException(exception))
            {
                Trace.TraceWarning("Stored Cotton token external payload cleanup failed: {0}", exception);
            }
        }

        private static TokenPairDto Clone(TokenPairDto tokens)
        {
            return new TokenPairDto
            {
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
            };
        }

        private static bool IsExternalSecretCleanupException(Exception exception)
        {
            return exception is CryptographicException
                or PlatformNotSupportedException
                or IOException
                or UnauthorizedAccessException;
        }

        private static void RestrictFileAccess(string path)
        {
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private void EnsureDirectoryExists()
        {
            string? directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}
