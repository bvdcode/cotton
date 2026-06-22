// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity
{
    /// <summary>
    /// Base class for descriptors that sign one protected EF row type.
    /// </summary>
    /// <remarks>
    /// The base class writes the common entity header and enforces the typed descriptor boundary. Concrete descriptors
    /// only decide which domain fields are security-sensitive enough to enter the canonical payload.
    /// </remarks>
    /// <typeparam name="T">The EF entity type represented by the descriptor.</typeparam>
    public abstract class DatabaseIntegrityDescriptor<T> : IDatabaseIntegrityDescriptor<T>
    {
        /// <inheritdoc />
        public Type EntityType => typeof(T);
        /// <inheritdoc />
        public abstract string EntityName { get; }
        /// <inheritdoc />
        public abstract int SchemaVersion { get; }

        /// <inheritdoc />
        public abstract string GetEntityKey(T entity);

        /// <inheritdoc />
        public string GetEntityKey(object entity)
        {
            return GetEntityKey(Cast(entity));
        }

        /// <inheritdoc />
        public byte[] BuildCanonicalPayload(object entity)
        {
            T typedEntity = Cast(entity);
            return DatabaseIntegrityCanonicalWriter.Build(writer =>
            {
                writer.WriteEntityHeader(EntityName, SchemaVersion, GetEntityKey(typedEntity));
                WriteCanonicalData(writer, typedEntity);
            });
        }

        /// <inheritdoc />
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
}
