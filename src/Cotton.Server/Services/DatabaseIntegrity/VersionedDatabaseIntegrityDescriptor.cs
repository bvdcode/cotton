// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity
{
    internal class VersionedDatabaseIntegrityDescriptor<T>(DatabaseIntegrityDescriptor<T> descriptor, int version)
        : DatabaseIntegrityDescriptor<T> where T : class
    {
        public override string EntityName => descriptor.EntityName;

        public override int SchemaVersion => version;

        public override IReadOnlyCollection<int> SupportedVersions => descriptor.SupportedVersions;

        public override IDatabaseIntegrityDescriptor Latest => descriptor.Latest;

        public override IDatabaseIntegrityDescriptor ForVersion(int requestedVersion) => descriptor.ForVersion(requestedVersion);

        public override string GetEntityKey(T entity) => descriptor.GetEntityKey(entity);

        public override void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, T entity, int requestedVersion)
        {
            descriptor.WriteCanonicalData(writer, entity, requestedVersion);
        }
    }
}
