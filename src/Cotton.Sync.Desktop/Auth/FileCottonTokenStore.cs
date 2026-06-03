// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Cotton.Contracts.Auth;
using Cotton.Sdk.Auth;

namespace Cotton.Sync.Desktop.Auth;

internal sealed class FileCottonTokenStore : ICottonTokenStore
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
            string tempPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                StoredTokenEnvelope envelope = await CreateEnvelopeAsync(tokens, cancellationToken).ConfigureAwait(false);
                await using (FileStream stream = File.Create(tempPath))
                {
                    await JsonSerializer.SerializeAsync(stream, envelope, JsonOptions, cancellationToken)
                        .ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                RestrictFileAccess(tempPath);
                File.Move(tempPath, _path, overwrite: true);
                RestrictFileAccess(_path);
            }
            finally
            {
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
            DeleteIfExists(_path);
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

    private static TokenPairDto Clone(TokenPairDto tokens)
    {
        return new TokenPairDto
        {
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
        };
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
