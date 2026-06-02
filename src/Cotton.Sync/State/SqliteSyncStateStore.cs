// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Cotton.Sync.State;

/// <summary>
/// Persists sync baselines in a SQLite database.
/// </summary>
public sealed class SqliteSyncStateStore : ISyncStateStore
{
    private const string DateTimeFormat = "O";
    private readonly string _databasePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteSyncStateStore" /> class.
    /// </summary>
    public SqliteSyncStateStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = databasePath;
    }

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        EnsureDirectoryExists();
        await using SqliteConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS sync_entries (
                sync_pair_id TEXT NOT NULL,
                relative_path_key TEXT NOT NULL,
                relative_path TEXT NOT NULL,
                kind INTEGER NOT NULL,
                local_content_hash TEXT NULL,
                local_last_write_utc TEXT NULL,
                remote_node_id TEXT NULL,
                remote_file_id TEXT NULL,
                remote_content_hash TEXT NULL,
                remote_etag TEXT NULL,
                synced_at_utc TEXT NOT NULL,
                PRIMARY KEY (sync_pair_id, relative_path_key)
            );
            """,
            cancellationToken).ConfigureAwait(false);
        await ExecuteNonQueryAsync(
            connection,
            "CREATE INDEX IF NOT EXISTS ix_sync_entries_remote_file_id ON sync_entries(remote_file_id);",
            cancellationToken).ConfigureAwait(false);
        await ExecuteNonQueryAsync(
            connection,
            "CREATE INDEX IF NOT EXISTS ix_sync_entries_remote_node_id ON sync_entries(remote_node_id);",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SyncStateEntry>> LoadPairAsync(string syncPairId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(syncPairId);
        await using SqliteConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT sync_pair_id, relative_path, kind, local_content_hash, local_last_write_utc,
                   remote_node_id, remote_file_id, remote_content_hash, remote_etag, synced_at_utc
            FROM sync_entries
            WHERE sync_pair_id = $sync_pair_id
            ORDER BY relative_path_key;
            """;
        command.Parameters.AddWithValue("$sync_pair_id", syncPairId);
        var entries = new List<SyncStateEntry>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            entries.Add(ReadEntry(reader));
        }

        return entries;
    }

    /// <inheritdoc />
    public async Task<SyncStateEntry?> GetAsync(string syncPairId, string relativePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(syncPairId);
        string key = SyncPath.ToKey(relativePath);
        await using SqliteConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT sync_pair_id, relative_path, kind, local_content_hash, local_last_write_utc,
                   remote_node_id, remote_file_id, remote_content_hash, remote_etag, synced_at_utc
            FROM sync_entries
            WHERE sync_pair_id = $sync_pair_id AND relative_path_key = $relative_path_key;
            """;
        command.Parameters.AddWithValue("$sync_pair_id", syncPairId);
        command.Parameters.AddWithValue("$relative_path_key", key);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadEntry(reader)
            : null;
    }

    /// <inheritdoc />
    public async Task UpsertAsync(SyncStateEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.SyncPairId);
        entry.RelativePath = SyncPath.Normalize(entry.RelativePath);
        if (entry.SyncedAtUtc == default)
        {
            entry.SyncedAtUtc = DateTime.UtcNow;
        }

        await using SqliteConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, entry, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string syncPairId, string relativePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(syncPairId);
        string key = SyncPath.ToKey(relativePath);
        await using SqliteConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM sync_entries WHERE sync_pair_id = $sync_pair_id AND relative_path_key = $relative_path_key;";
        command.Parameters.AddWithValue("$sync_pair_id", syncPairId);
        command.Parameters.AddWithValue("$relative_path_key", key);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ReplacePairAsync(
        string syncPairId,
        IReadOnlyCollection<SyncStateEntry> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(syncPairId);
        ArgumentNullException.ThrowIfNull(entries);
        await using SqliteConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteTransaction transaction = connection.BeginTransaction();
        await using SqliteCommand delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = "DELETE FROM sync_entries WHERE sync_pair_id = $sync_pair_id;";
        delete.Parameters.AddWithValue("$sync_pair_id", syncPairId);
        await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        foreach (SyncStateEntry entry in entries)
        {
            entry.SyncPairId = syncPairId;
            entry.RelativePath = SyncPath.Normalize(entry.RelativePath);
            await UpsertAsync(connection, entry, cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertAsync(
        SqliteConnection connection,
        SyncStateEntry entry,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO sync_entries (
                sync_pair_id, relative_path_key, relative_path, kind, local_content_hash, local_last_write_utc,
                remote_node_id, remote_file_id, remote_content_hash, remote_etag, synced_at_utc)
            VALUES (
                $sync_pair_id, $relative_path_key, $relative_path, $kind, $local_content_hash, $local_last_write_utc,
                $remote_node_id, $remote_file_id, $remote_content_hash, $remote_etag, $synced_at_utc)
            ON CONFLICT(sync_pair_id, relative_path_key) DO UPDATE SET
                relative_path = excluded.relative_path,
                kind = excluded.kind,
                local_content_hash = excluded.local_content_hash,
                local_last_write_utc = excluded.local_last_write_utc,
                remote_node_id = excluded.remote_node_id,
                remote_file_id = excluded.remote_file_id,
                remote_content_hash = excluded.remote_content_hash,
                remote_etag = excluded.remote_etag,
                synced_at_utc = excluded.synced_at_utc;
            """;
        AddEntryParameters(command, entry);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AddEntryParameters(SqliteCommand command, SyncStateEntry entry)
    {
        command.Parameters.AddWithValue("$sync_pair_id", entry.SyncPairId);
        command.Parameters.AddWithValue("$relative_path_key", SyncPath.ToKey(entry.RelativePath));
        command.Parameters.AddWithValue("$relative_path", SyncPath.Normalize(entry.RelativePath));
        command.Parameters.AddWithValue("$kind", (int)entry.Kind);
        AddNullableString(command, "$local_content_hash", entry.LocalContentHash);
        AddNullableString(command, "$local_last_write_utc", FormatDateTime(entry.LocalLastWriteUtc));
        AddNullableString(command, "$remote_node_id", entry.RemoteNodeId?.ToString("D"));
        AddNullableString(command, "$remote_file_id", entry.RemoteFileId?.ToString("D"));
        AddNullableString(command, "$remote_content_hash", entry.RemoteContentHash);
        AddNullableString(command, "$remote_etag", entry.RemoteETag);
        command.Parameters.AddWithValue("$synced_at_utc", FormatDateTime(entry.SyncedAtUtc)!);
    }

    private static SyncStateEntry ReadEntry(SqliteDataReader reader)
    {
        return new SyncStateEntry
        {
            SyncPairId = reader.GetString(0),
            RelativePath = reader.GetString(1),
            Kind = (SyncEntryKind)reader.GetInt32(2),
            LocalContentHash = GetNullableString(reader, 3),
            LocalLastWriteUtc = ParseDateTime(GetNullableString(reader, 4)),
            RemoteNodeId = ParseGuid(GetNullableString(reader, 5)),
            RemoteFileId = ParseGuid(GetNullableString(reader, 6)),
            RemoteContentHash = GetNullableString(reader, 7),
            RemoteETag = GetNullableString(reader, 8),
            SyncedAtUtc = ParseDateTime(reader.GetString(9)) ?? DateTime.UtcNow,
        };
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection($"Data Source={_databasePath};Mode=ReadWriteCreate");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private void EnsureDirectoryExists()
    {
        string? directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AddNullableString(SqliteCommand command, string name, string? value)
    {
        command.Parameters.AddWithValue(name, string.IsNullOrEmpty(value) ? DBNull.Value : value);
    }

    private static string? GetNullableString(SqliteDataReader reader, int index)
    {
        return reader.IsDBNull(index) ? null : reader.GetString(index);
    }

    private static string? FormatDateTime(DateTime? value)
    {
        return value?.ToUniversalTime().ToString(DateTimeFormat, CultureInfo.InvariantCulture);
    }

    private static DateTime? ParseDateTime(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();
    }

    private static Guid? ParseGuid(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : Guid.Parse(value);
    }
}
