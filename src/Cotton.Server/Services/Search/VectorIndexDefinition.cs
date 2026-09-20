// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Npgsql.Models;

namespace Cotton.Server.Services.Search
{
    public static class VectorIndexDefinition
    {
        public const int Version = 1;
        public const int Dimensions = 1024;
        public const string ModelId = "BAAI/bge-m3";
        public const string Pooling = "cls";

        public static PostgresVectorIndexDefinition Expected { get; } = new(
            SchemaName: "public",
            TableName: "file_embeddings",
            IndexName: "ix_file_embeddings_bge_m3_v1",
            VectorColumnName: "embedding",
            Dimensions: Dimensions,
            FilterColumnName: "index_version",
            FilterValue: Version);

        public static string ManualCreateSql => FormattableString.Invariant($"""
            CREATE INDEX CONCURRENTLY IF NOT EXISTS {Expected.IndexName}
            ON {Expected.SchemaName}.{Expected.TableName}
            USING hnsw (({Expected.VectorColumnName}::vector({Expected.Dimensions})) vector_cosine_ops)
            WHERE {Expected.FilterColumnName} = {Expected.FilterValue}
            """);

        public static bool IsReady(PostgresIndexStatus status) => status.IsValid && status.IsCompatibleWith(Expected);
    }
}
