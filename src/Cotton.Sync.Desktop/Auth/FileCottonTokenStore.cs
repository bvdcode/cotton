// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

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

    public FileCottonTokenStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
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

            await using FileStream stream = File.OpenRead(_path);
            TokenPairDto? tokens = await JsonSerializer
                .DeserializeAsync<TokenPairDto>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return tokens is not null && IsUsable(tokens) ? Clone(tokens) : null;
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
                await using (FileStream stream = File.Create(tempPath))
                {
                    await JsonSerializer.SerializeAsync(stream, Clone(tokens), JsonOptions, cancellationToken)
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
