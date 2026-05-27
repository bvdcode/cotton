// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.KeyManagement;

/// <summary>
/// File-system replica for small encrypted keyring system objects.
/// </summary>
internal sealed class KeyringLocalFileReplica(string _rootPath, string? _name = null) : IKeyringObjectReplica
{
    private const string TempExtension = ".tmp";

    public string Name { get; } = string.IsNullOrWhiteSpace(_name) ? "local-file" : _name;

    public async Task WriteAsync(string name, byte[] bytes, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(bytes);

        string path = ResolvePath(name);
        string directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Keyring object path has no parent directory.");
        Directory.CreateDirectory(directory);

        string tmpPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}{TempExtension}");
        try
        {
            await using (var stream = new FileStream(
                tmpPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16 * 1024,
                options: FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tmpPath, path, overwrite: true);
        }
        catch
        {
            TryDelete(tmpPath);
            throw;
        }
    }

    public async Task<byte[]?> TryReadAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string path = ResolvePath(name);
        if (!File.Exists(path))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(path, cancellationToken);
    }

    public async IAsyncEnumerable<string> ListNamesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_rootPath))
        {
            yield break;
        }

        foreach (string path in Directory.EnumerateFiles(_rootPath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (path.EndsWith(TempExtension, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return Path.GetRelativePath(_rootPath, path)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
        }

        await Task.CompletedTask;
    }

    private string ResolvePath(string name)
    {
        string normalized = NormalizeObjectName(name);
        string relativePath = normalized.Replace('/', Path.DirectorySeparatorChar);
        string root = Path.GetFullPath(_rootPath);
        string path = Path.GetFullPath(Path.Combine(root, relativePath));
        string rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new ArgumentException("Keyring object name escapes the replica root.", nameof(name));
        }

        return path;
    }

    private static string NormalizeObjectName(string name)
    {
        string normalized = name.Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.Contains("../", StringComparison.Ordinal)
            || normalized.Contains("/..", StringComparison.Ordinal)
            || normalized == "..")
        {
            throw new ArgumentException("Invalid keyring object name.", nameof(name));
        }

        return normalized;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}
