// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Cotton.Server.Services.KeyManagement;

/// <summary>
/// Type of immutable keyring object tracked by the journaled store.
/// </summary>
internal enum KeyringObjectKind
{
    AccessEnvelope,
    StateSnapshot
}

/// <summary>
/// Logical pointer to a committed immutable keyring object.
/// </summary>
internal sealed record KeyringObjectPointer(
    KeyringObjectKind Kind,
    int Generation,
    string Hash,
    string ObjectName,
    DateTimeOffset CommittedAtUtc);

/// <summary>
/// Immutable keyring object read from a valid replica.
/// </summary>
internal sealed record KeyringLoadedObject(KeyringObjectPointer Pointer, byte[] Bytes, string ReplicaName);

/// <summary>
/// A small byte-object replica for keyring system artifacts.
/// </summary>
internal interface IKeyringObjectReplica
{
    string Name { get; }

    Task WriteAsync(string name, byte[] bytes, CancellationToken cancellationToken = default);

    Task<byte[]?> TryReadAsync(string name, CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> ListNamesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Append-only keyring object store. Heads are authoritative; latest is a disposable cache.
/// </summary>
internal sealed class KeyringJournaledObjectStore
{
    private readonly IReadOnlyList<IKeyringObjectReplica> _replicas;

    public KeyringJournaledObjectStore(IEnumerable<IKeyringObjectReplica> replicas)
    {
        _replicas = replicas.ToArray();
        if (_replicas.Count == 0)
        {
            throw new ArgumentException("At least one keyring replica is required.", nameof(replicas));
        }
    }

    public async Task<KeyringObjectPointer> CommitAsync(
        KeyringObjectKind kind,
        int generation,
        byte[] canonicalBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(generation);
        ArgumentNullException.ThrowIfNull(canonicalBytes);

        string hash = ToSha256Hex(canonicalBytes);
        string objectName = KeyringObjectNames.GetObjectName(kind, generation, hash!);
        var pointer = new KeyringObjectPointer(kind, generation, hash, objectName, DateTimeOffset.UtcNow);
        byte[] pointerBytes = KeyringJson.SerializeToUtf8Bytes(pointer);

        foreach (IKeyringObjectReplica replica in _replicas)
        {
            await replica.WriteAsync(objectName, canonicalBytes, cancellationToken);
        }

        await RequireReadableFromAnyReplicaAsync(pointer, cancellationToken);

        string headName = KeyringObjectNames.GetHeadName(kind, generation, hash!);
        foreach (IKeyringObjectReplica replica in _replicas)
        {
            await replica.WriteAsync(headName, pointerBytes, cancellationToken);
        }

        string latestName = KeyringObjectNames.GetLatestName(kind);
        foreach (IKeyringObjectReplica replica in _replicas)
        {
            await replica.WriteAsync(latestName, pointerBytes, cancellationToken);
        }

        return pointer;
    }

    public async Task<KeyringLoadedObject?> FindLatestValidAsync(
        KeyringObjectKind kind,
        CancellationToken cancellationToken = default)
    {
        List<KeyringObjectPointer> candidates = await ListHeadPointersAsync(kind, cancellationToken);
        foreach (KeyringObjectPointer pointer in candidates
            .OrderByDescending(x => x.Generation)
            .ThenByDescending(x => x.CommittedAtUtc))
        {
            KeyringLoadedObject? loaded = await TryReadValidObjectAsync(pointer, cancellationToken);
            if (loaded is not null)
            {
                return loaded;
            }
        }

        return null;
    }

    private async Task<List<KeyringObjectPointer>> ListHeadPointersAsync(
        KeyringObjectKind kind,
        CancellationToken cancellationToken)
    {
        Dictionary<string, KeyringObjectPointer> pointers = new(StringComparer.OrdinalIgnoreCase);
        foreach (IKeyringObjectReplica replica in _replicas)
        {
            await foreach (string name in replica.ListNamesAsync(cancellationToken))
            {
                if (!KeyringObjectNames.TryParseHeadName(name, kind, out int generation, out string? hash))
                {
                    continue;
                }

                string pointerKey = $"{generation}:{hash}";
                if (pointers.ContainsKey(pointerKey))
                {
                    continue;
                }

                string headName = KeyringObjectNames.GetHeadName(kind, generation, hash!);
                byte[]? bytes = await replica.TryReadAsync(headName, cancellationToken);
                if (bytes is null)
                {
                    continue;
                }

                try
                {
                    KeyringObjectPointer pointer = KeyringJson.Deserialize<KeyringObjectPointer>(bytes);
                    if (pointer.Kind == kind
                        && pointer.Generation == generation
                        && string.Equals(pointer.Hash, hash, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(
                            pointer.ObjectName,
                            KeyringObjectNames.GetObjectName(kind, generation, hash!),
                            StringComparison.Ordinal))
                    {
                        pointers[pointerKey] = pointer;
                    }
                }
                catch (Exception ex) when (ex is JsonException or InvalidDataException)
                {
                    continue;
                }
            }
        }

        return pointers.Values.ToList();
    }

    private async Task RequireReadableFromAnyReplicaAsync(
        KeyringObjectPointer pointer,
        CancellationToken cancellationToken)
    {
        KeyringLoadedObject? loaded = await TryReadValidObjectAsync(pointer, cancellationToken);
        if (loaded is null)
        {
            throw new IOException($"Committed keyring object {pointer.ObjectName} could not be verified from any replica.");
        }
    }

    private async Task<KeyringLoadedObject?> TryReadValidObjectAsync(
        KeyringObjectPointer pointer,
        CancellationToken cancellationToken)
    {
        foreach (IKeyringObjectReplica replica in _replicas)
        {
            byte[]? bytes = await replica.TryReadAsync(pointer.ObjectName, cancellationToken);
            if (bytes is null)
            {
                continue;
            }

            string actualHash = ToSha256Hex(bytes);
            if (string.Equals(actualHash, pointer.Hash, StringComparison.OrdinalIgnoreCase))
            {
                return new KeyringLoadedObject(pointer, bytes, replica.Name);
            }
        }

        return null;
    }

    private static string ToSha256Hex(byte[] bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}

internal static partial class KeyringObjectNames
{
    private const string Prefix = ".cotton/system/keyring/v2";

    public static string GetObjectName(KeyringObjectKind kind, int generation, string hash)
    {
        string segment = GetKindSegment(kind);
        return $"{Prefix}/{segment}/{generation:D20}-{hash}.json";
    }

    public static string GetHeadName(KeyringObjectKind kind, int generation, string hash)
    {
        string segment = GetKindSegment(kind);
        return $"{Prefix}/heads/{segment}/{generation:D20}-{hash}.head";
    }

    public static string GetLatestName(KeyringObjectKind kind)
    {
        string segment = GetKindSegment(kind);
        return $"{Prefix}/latest/{segment}.json";
    }

    public static bool TryParseHeadName(
        string name,
        KeyringObjectKind expectedKind,
        out int generation,
        out string? hash)
    {
        generation = 0;
        hash = null;

        string segment = GetKindSegment(expectedKind);
        Match match = HeadNameRegex().Match(name);
        if (!match.Success
            || !string.Equals(match.Groups["segment"].Value, segment, StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(match.Groups["generation"].Value, out generation))
        {
            return false;
        }

        hash = match.Groups["hash"].Value;
        return true;
    }

    private static string GetKindSegment(KeyringObjectKind kind)
    {
        return kind switch
        {
            KeyringObjectKind.AccessEnvelope => "access",
            KeyringObjectKind.StateSnapshot => "state",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    [GeneratedRegex("^\\.cotton/system/keyring/v2/heads/(?<segment>access|state)/(?<generation>[0-9]{20})-(?<hash>[0-9a-f]{64})\\.head$", RegexOptions.CultureInvariant)]
    private static partial Regex HeadNameRegex();
}

