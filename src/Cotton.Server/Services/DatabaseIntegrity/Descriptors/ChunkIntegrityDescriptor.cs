// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using EasyExtensions.EntityFrameworkCore.Database;

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors
{
    /// <summary>
    /// Describes storage chunk identity and immutable serving metadata.
    /// </summary>
    /// <remarks>
    /// The GC schedule is intentionally excluded: it is mutable housekeeping state and does not affect the bytes a reader
    /// receives. File download verification checks chunk references and sizes through the manifest graph instead.
    /// </remarks>
    public class ChunkIntegrityDescriptor : DatabaseIntegrityDescriptor<Chunk>
    {
        /// <inheritdoc />
        public override string EntityName => "chunks";
        /// <inheritdoc />
        public override int SchemaVersion => 1;

        /// <inheritdoc />
        public override string GetEntityKey(Chunk entity)
        {
            return Hasher.ToHexStringHash(entity.Hash);
        }

        /// <inheritdoc />
        public override void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, Chunk entity)
        {
            writer.WriteBytesField(nameof(entity.Hash), entity.Hash);
            writer.WriteInt64Field(nameof(entity.PlainSizeBytes), entity.PlainSizeBytes);
            writer.WriteInt64Field(nameof(entity.StoredSizeBytes), entity.StoredSizeBytes);
            writer.WriteInt32Field(nameof(entity.CompressionAlgorithm), (int)entity.CompressionAlgorithm);
        }
    }
}
