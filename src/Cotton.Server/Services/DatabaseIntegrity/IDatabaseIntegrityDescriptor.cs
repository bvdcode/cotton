// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity;

public interface IDatabaseIntegrityDescriptor
{
    Type EntityType { get; }
    string EntityName { get; }
    int SchemaVersion { get; }
    string GetEntityKey(object entity);
    byte[] BuildCanonicalPayload(object entity);
}

public interface IDatabaseIntegrityDescriptor<in T> : IDatabaseIntegrityDescriptor
{
    string GetEntityKey(T entity);
    void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, T entity);
}
