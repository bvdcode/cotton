// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;
using System.Security.Cryptography;
using System.Text;

namespace Cotton.Server.Services.KeyManagement;

/// <summary>
/// Object-storage replica for small encrypted keyring system objects.
/// </summary>
internal sealed class KeyringStorageBackendReplica(IStorageBackend _backend, string? _name = null) : IKeyringObjectReplica
{
    private const string PayloadMagic = "cotton.keyring-storage-replica.v2";
    private const string UidPurpose = "cotton/keyring-storage-replica/v2|";

    public string Name { get; } = string.IsNullOrWhiteSpace(_name) ? "object-storage" : _name;

    public async Task WriteAsync(string name, byte[] bytes, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(bytes);

        string uid = GetStorageUid(name);
        byte[] payload = KeyringJson.SerializeToUtf8Bytes(new KeyringStoragePayload(
            PayloadMagic,
            name,
            Convert.ToBase64String(bytes)));
        await using var stream = new MemoryStream(payload, writable: false);

        // Most chunk backends are immutable/deduplicating. Delete first so mutable pointers
        // such as latest/* can be refreshed while immutable objects remain naturally idempotent.
        await _backend.DeleteAsync(uid);
        await _backend.WriteAsync(uid, stream);
    }

    public async Task<byte[]?> TryReadAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string uid = GetStorageUid(name);
        if (!await _backend.ExistsAsync(uid))
        {
            return null;
        }

        await using Stream stream = await _backend.ReadAsync(uid);
        KeyringStoragePayload? payload = await TryReadPayloadAsync(stream, cancellationToken);
        if (payload is null
            || payload.Magic != PayloadMagic
            || !string.Equals(payload.Name, name, StringComparison.Ordinal))
        {
            return null;
        }

        return Convert.FromBase64String(payload.BytesBase64);
    }

    public async IAsyncEnumerable<string> ListNamesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (string uid in _backend.ListAllKeysAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            KeyringStoragePayload? payload = null;
            try
            {
                await using Stream stream = await _backend.ReadAsync(uid);
                payload = await TryReadPayloadAsync(stream, cancellationToken);
            }
            catch (Exception ex) when (ex is FileNotFoundException or FormatException or InvalidDataException)
            {
            }

            if (payload?.Magic == PayloadMagic
                && !string.IsNullOrWhiteSpace(payload.Name)
                && string.Equals(uid, GetStorageUid(payload.Name), StringComparison.OrdinalIgnoreCase))
            {
                yield return payload.Name;
            }
        }
    }

    private static async Task<KeyringStoragePayload?> TryReadPayloadAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        try
        {
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);
            return KeyringJson.Deserialize<KeyringStoragePayload>(memory.ToArray());
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidDataException)
        {
            return null;
        }
    }

    internal static string GetStorageUid(string name)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(UidPurpose + name);
        try
        {
            return Convert.ToHexStringLower(SHA256.HashData(bytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private sealed record KeyringStoragePayload(
        string Magic,
        string Name,
        string BytesBase64);
}
