// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Routines;
using System.Text.Json;

namespace Cotton.Server.Services.DatabaseIntegrity.Descriptors
{
    public class RoutineIntegrityDescriptor : DatabaseIntegrityDescriptor<Routine>
    {
        public override string EntityName => "routines";

        public override int SchemaVersion => 1;

        public override string GetEntityKey(Routine entity) => entity.Id.ToString("D");

        public override void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, Routine entity, int version)
        {
            writer.WriteGuidField(nameof(entity.Id), entity.Id);
            writer.WriteGuidField(nameof(entity.OwnerId), entity.OwnerId);
            writer.WriteStringField(nameof(entity.Name), entity.Name);
            writer.WriteInt32Field(nameof(entity.Type), (int)entity.Type);
            writer.WriteBooleanField(nameof(entity.Enabled), entity.Enabled);
            writer.WriteGuidField(nameof(entity.TargetNodeId), entity.TargetNodeId);
            writer.WriteInt64Field(nameof(entity.Interval), entity.Interval.Ticks);
            writer.WriteNullableDateTimeField(nameof(entity.LastExecutionAt), entity.LastExecutionAt);
            switch (entity.Type)
            {
                case RoutineType.DeleteFiles:
                    DeleteFilesRoutineSettings settings = JsonSerializer.Deserialize<DeleteFilesRoutineSettings>(entity.Settings)
                        ?? throw new InvalidOperationException("Routine settings are required.");
                    writer.WriteInt32Field(nameof(settings.OlderThanDays), settings.OlderThanDays);
                    writer.WriteInt32Field(nameof(settings.DateField), (int)settings.DateField);
                    writer.WriteBooleanField(nameof(settings.IncludeSubfolders), settings.IncludeSubfolders);
                    writer.WriteBooleanField(nameof(settings.PermanentlyDelete), settings.PermanentlyDelete);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported routine type.");
            }
        }
    }
}
