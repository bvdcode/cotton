// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.IntegrationTests.Helpers
{
    public class IntegrityTestEntityDescriptor : DatabaseIntegrityDescriptor<IntegrityTestEntity>
    {
        public override string EntityName => "test_entity";
        public override int SchemaVersion => 1;

        public override string GetEntityKey(IntegrityTestEntity entity)
        {
            return entity.Id.ToString("D");
        }

        public override void WriteCanonicalData(
            DatabaseIntegrityCanonicalWriter writer,
            IntegrityTestEntity entity,
            int version)
        {
            writer.WriteGuidField(nameof(entity.Id), entity.Id);
            writer.WriteNullableGuidField(nameof(entity.OwnerId), entity.OwnerId);
            writer.WriteStringField(nameof(entity.Name), entity.Name);
            writer.WriteInt64Field(nameof(entity.SizeBytes), entity.SizeBytes);
            writer.WriteBooleanField(nameof(entity.IsEnabled), entity.IsEnabled);
            writer.WriteNullableDateTimeField(nameof(entity.SeenAt), entity.SeenAt);
            writer.WriteStringArrayField(nameof(entity.Transports), entity.Transports);
            writer.WriteStringDictionaryField(nameof(entity.Metadata), entity.Metadata);
        }
    }
}
