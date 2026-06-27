// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Buffers.Binary;
using System.Text;

namespace Cotton.Server.Services.DatabaseIntegrity
{
    /// <summary>
    /// Writes database row fields into a deterministic binary representation before HMAC signing.
    /// </summary>
    /// <remarks>
    /// The format is intentionally boring: UTF-8 strings are length-prefixed, integers are big-endian, nullable values
    /// carry an explicit presence byte, and dictionaries are key-sorted. That keeps signatures stable across machines,
    /// database providers, and JSON serializer changes.
    /// </remarks>
    public class DatabaseIntegrityCanonicalWriter
    {
        private readonly MemoryStream _stream = new();

        /// <summary>
        /// Builds one canonical payload and prefixes it with the row-integrity format marker.
        /// </summary>
        public static byte[] Build(Action<DatabaseIntegrityCanonicalWriter> write)
        {
            ArgumentNullException.ThrowIfNull(write);

            DatabaseIntegrityCanonicalWriter writer = new();
            writer.WriteBytes(Encoding.ASCII.GetBytes(DatabaseIntegritySignatureContract.PayloadMagic));
            writer.WriteInt32Field("$payload-format", DatabaseIntegritySignatureContract.PayloadFormatVersion);
            writer.WriteStringField("$mac", DatabaseIntegritySignatureContract.MacAlgorithm);
            writer.WriteInt32Field("$canonical-writer", DatabaseIntegritySignatureContract.CanonicalWriterVersion);
            write(writer);
            return writer._stream.ToArray();
        }

        /// <summary>
        /// Writes the common entity identity fields included in every protected row signature.
        /// </summary>
        public void WriteEntityHeader(string entityName, int schemaVersion, string entityKey)
        {
            WriteStringField("$entity", entityName);
            WriteInt32Field("$schema", schemaVersion);
            WriteStringField("$key", entityKey);
        }

        /// <summary>
        /// Writes a nullable UTF-8 string field.
        /// </summary>
        public void WriteStringField(string name, string? value)
        {
            WriteFieldHeader(name, DatabaseIntegrityFieldType.String);
            WriteNullableReference(value, static (writer, item) => writer.WriteString(item));
        }

        /// <summary>
        /// Writes a non-null GUID field using the canonical dashed representation.
        /// </summary>
        public void WriteGuidField(string name, Guid value)
        {
            WriteFieldHeader(name, DatabaseIntegrityFieldType.Guid);
            WriteString(value.ToString("D"));
        }

        /// <summary>
        /// Writes a nullable GUID field using the canonical dashed representation.
        /// </summary>
        public void WriteNullableGuidField(string name, Guid? value)
        {
            WriteFieldHeader(name, DatabaseIntegrityFieldType.Guid);
            WriteNullableValue(value, static (writer, item) => writer.WriteString(item.ToString("D")));
        }

        /// <summary>
        /// Writes a nullable binary field.
        /// </summary>
        public void WriteBytesField(string name, byte[]? value)
        {
            WriteFieldHeader(name, DatabaseIntegrityFieldType.Bytes);
            WriteNullableReference(value, static (writer, item) => writer.WriteBytesWithLength(item));
        }

        /// <summary>
        /// Writes a Boolean field as a single byte.
        /// </summary>
        public void WriteBooleanField(string name, bool value)
        {
            WriteFieldHeader(name, DatabaseIntegrityFieldType.Boolean);
            _stream.WriteByte(value ? (byte)1 : (byte)0);
        }

        /// <summary>
        /// Writes a 32-bit integer field in big-endian order.
        /// </summary>
        public void WriteInt32Field(string name, int value)
        {
            WriteFieldHeader(name, DatabaseIntegrityFieldType.Int32);
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(buffer, value);
            WriteBytes(buffer);
        }

        /// <summary>
        /// Writes a 64-bit integer field in big-endian order.
        /// </summary>
        public void WriteInt64Field(string name, long value)
        {
            WriteFieldHeader(name, DatabaseIntegrityFieldType.Int64);
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            BinaryPrimitives.WriteInt64BigEndian(buffer, value);
            WriteBytes(buffer);
        }

        /// <summary>
        /// Writes a nullable <see cref="DateTime"/> normalized to UTC microsecond ticks.
        /// </summary>
        public void WriteNullableDateTimeField(string name, DateTime? value)
        {
            WriteFieldHeader(name, DatabaseIntegrityFieldType.DateTime);
            WriteNullableValue(value, static (writer, item) =>
            {
                DateTime utc = item.Kind == DateTimeKind.Utc ? item : item.ToUniversalTime();
                writer.WriteInt64(NormalizeToDatabasePrecision(utc).Ticks);
            });
        }

        private static DateTime NormalizeToDatabasePrecision(DateTime utc)
        {
            long ticks = utc.Ticks - (utc.Ticks % TimeSpan.TicksPerMicrosecond);
            return new DateTime(ticks, DateTimeKind.Utc);
        }

        /// <summary>
        /// Writes a nullable ordered string collection exactly as supplied by the descriptor.
        /// </summary>
        public void WriteStringArrayField(string name, IReadOnlyCollection<string>? values)
        {
            WriteFieldHeader(name, DatabaseIntegrityFieldType.StringArray);
            WriteNullableReference(values, static (writer, items) =>
            {
                writer.WriteInt32(items.Count);
                foreach (string item in items)
                {
                    writer.WriteString(item);
                }
            });
        }

        /// <summary>
        /// Writes a nullable string dictionary sorted by ordinal key for stable signatures.
        /// </summary>
        public void WriteStringDictionaryField(string name, IReadOnlyDictionary<string, string>? values)
        {
            WriteFieldHeader(name, DatabaseIntegrityFieldType.StringDictionary);
            WriteNullableReference(values, static (writer, items) =>
            {
                writer.WriteInt32(items.Count);
                foreach (KeyValuePair<string, string> item in items.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    writer.WriteString(item.Key);
                    writer.WriteString(item.Value);
                }
            });
        }

        private void WriteFieldHeader(string name, DatabaseIntegrityFieldType fieldType)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Field name cannot be empty.", nameof(name));
            }

            WriteString(name);
            _stream.WriteByte((byte)fieldType);
        }

        private void WriteNullableReference<T>(T? value, Action<DatabaseIntegrityCanonicalWriter, T> write)
            where T : class
        {
            if (value is null)
            {
                _stream.WriteByte(0);
                return;
            }

            _stream.WriteByte(1);
            write(this, value);
        }

        private void WriteNullableValue<T>(T? value, Action<DatabaseIntegrityCanonicalWriter, T> write)
            where T : struct
        {
            if (!value.HasValue)
            {
                _stream.WriteByte(0);
                return;
            }

            _stream.WriteByte(1);
            write(this, value.Value);
        }

        private void WriteString(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            WriteBytesWithLength(bytes);
        }

        private void WriteBytesWithLength(ReadOnlySpan<byte> bytes)
        {
            WriteInt32(bytes.Length);
            WriteBytes(bytes);
        }

        private void WriteInt32(int value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(buffer, value);
            WriteBytes(buffer);
        }

        private void WriteInt64(long value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            BinaryPrimitives.WriteInt64BigEndian(buffer, value);
            WriteBytes(buffer);
        }

        private void WriteBytes(ReadOnlySpan<byte> bytes)
        {
            _stream.Write(bytes);
        }
    }
}
