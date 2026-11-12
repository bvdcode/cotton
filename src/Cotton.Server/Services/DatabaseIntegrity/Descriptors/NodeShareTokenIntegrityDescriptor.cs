// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors
{
    /// <summary>
    /// Describes public share-token rows protected by database integrity signing.
    /// </summary>
    /// <remarks>
    /// The MAC prevents a database-only edit from retargeting a public link, extending its lifetime, or changing the user who
    /// created the share.
    /// </remarks>
    public class NodeShareTokenIntegrityDescriptor : DatabaseIntegrityDescriptor<NodeShareToken>
    {
        public const int LatestVersion = 3;

        public override string EntityName => "node_share_tokens";

        public override int SchemaVersion => LatestVersion;

        public override IReadOnlyCollection<int> SupportedVersions => [1, 2, 3];

        public override string GetEntityKey(NodeShareToken entity)
        {
            return entity.Id.ToString("D");
        }

        public override void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, NodeShareToken entity, int version)
        {
            switch (version)
            {
                case 1 when !entity.AllowUpload && !entity.HideFolderContent:
                    WriteVersion1(writer, entity);
                    break;
                case 1:
                    throw new DatabaseIntegrityException(EntityName, GetEntityKey(entity));
                case 2 when !entity.HideFolderContent:
                    WriteVersion2(writer, entity);
                    break;
                case 2:
                    throw new DatabaseIntegrityException(EntityName, GetEntityKey(entity));
                case 3:
                    WriteVersion2(writer, entity);
                    writer.WriteBooleanField(nameof(entity.HideFolderContent), entity.HideFolderContent);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(version), version, "Unsupported share signature version.");
            }
        }

        private static void WriteVersion2(DatabaseIntegrityCanonicalWriter writer, NodeShareToken entity)
        {
            WriteVersion1(writer, entity);
            writer.WriteBooleanField(nameof(entity.AllowUpload), entity.AllowUpload);
        }

        private static void WriteVersion1(DatabaseIntegrityCanonicalWriter writer, NodeShareToken entity)
        {
            writer.WriteGuidField(nameof(entity.Id), entity.Id);
            writer.WriteStringField(nameof(entity.Token), entity.Token);
            writer.WriteGuidField(nameof(entity.NodeId), entity.NodeId);
            writer.WriteGuidField(nameof(entity.CreatedByUserId), entity.CreatedByUserId);
            writer.WriteNullableDateTimeField(nameof(entity.ExpiresAt), entity.ExpiresAt);
        }
    }
}
