// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity;

public abstract class DatabaseIntegrityDescriptor<T> : IDatabaseIntegrityDescriptor<T>
{
    public Type EntityType => typeof(T);
    public abstract string EntityName { get; }
    public abstract int SchemaVersion { get; }

    public abstract string GetEntityKey(T entity);

    public string GetEntityKey(object entity)
    {
        return GetEntityKey(Cast(entity));
    }

    public byte[] BuildCanonicalPayload(object entity)
    {
        T typedEntity = Cast(entity);
        return DatabaseIntegrityCanonicalWriter.Build(writer =>
        {
            writer.WriteEntityHeader(EntityName, SchemaVersion, GetEntityKey(typedEntity));
            WriteCanonicalData(writer, typedEntity);
        });
    }

    public abstract void WriteCanonicalData(DatabaseIntegrityCanonicalWriter writer, T entity);

    private static T Cast(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (entity is not T typedEntity)
        {
            throw new ArgumentException(
                $"Expected entity of type {typeof(T).FullName}, got {entity.GetType().FullName}.",
                nameof(entity));
        }

        return typedEntity;
    }
}
