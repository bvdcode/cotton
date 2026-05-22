// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Buffers.Binary;
using System.Text;

namespace Cotton.Server.Services.DatabaseIntegrity;

public sealed class DatabaseIntegrityCanonicalWriter
{
    private static readonly byte[] FormatMagic = Encoding.ASCII.GetBytes("Cotton.DbIntegrity.Row.v1");
    private readonly MemoryStream _stream = new();

    public static byte[] Build(Action<DatabaseIntegrityCanonicalWriter> write)
    {
        ArgumentNullException.ThrowIfNull(write);

        var writer = new DatabaseIntegrityCanonicalWriter();
        writer.WriteBytes(FormatMagic);
        write(writer);
        return writer._stream.ToArray();
    }

    public void WriteEntityHeader(string entityName, int schemaVersion, string entityKey)
    {
        WriteStringField("$entity", entityName);
        WriteInt32Field("$schema", schemaVersion);
        WriteStringField("$key", entityKey);
    }

    public void WriteStringField(string name, string? value)
    {
        WriteFieldHeader(name, DatabaseIntegrityFieldType.String);
        WriteNullableReference(value, static (writer, item) => writer.WriteString(item));
    }

    public void WriteGuidField(string name, Guid value)
    {
        WriteFieldHeader(name, DatabaseIntegrityFieldType.Guid);
        WriteString(value.ToString("D"));
    }

    public void WriteNullableGuidField(string name, Guid? value)
    {
        WriteFieldHeader(name, DatabaseIntegrityFieldType.Guid);
        WriteNullableValue(value, static (writer, item) => writer.WriteString(item.ToString("D")));
    }

    public void WriteBytesField(string name, byte[]? value)
    {
        WriteFieldHeader(name, DatabaseIntegrityFieldType.Bytes);
        WriteNullableReference(value, static (writer, item) => writer.WriteBytesWithLength(item));
    }

    public void WriteBooleanField(string name, bool value)
    {
        WriteFieldHeader(name, DatabaseIntegrityFieldType.Boolean);
        _stream.WriteByte(value ? (byte)1 : (byte)0);
    }

    public void WriteInt32Field(string name, int value)
    {
        WriteFieldHeader(name, DatabaseIntegrityFieldType.Int32);
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        WriteBytes(buffer);
    }

    public void WriteInt64Field(string name, long value)
    {
        WriteFieldHeader(name, DatabaseIntegrityFieldType.Int64);
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(buffer, value);
        WriteBytes(buffer);
    }

    public void WriteNullableDateTimeField(string name, DateTime? value)
    {
        WriteFieldHeader(name, DatabaseIntegrityFieldType.DateTime);
        WriteNullableValue(value, static (writer, item) =>
        {
            DateTime utc = item.Kind == DateTimeKind.Utc ? item : item.ToUniversalTime();
            writer.WriteInt64(utc.Ticks);
        });
    }

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
