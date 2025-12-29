// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cotton.Server.Infrastructure
{
    public class JsonEnumArrayConverter<TEnum> : JsonConverter<TEnum[]> where TEnum : struct, Enum
    {
        public override TEnum[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException("Expected start of array");
            }

            var list = new List<TEnum>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    return list.ToArray();
                }

                if (reader.TokenType == JsonTokenType.String)
                {
                    var stringValue = reader.GetString();
                    if (stringValue != null && Enum.TryParse<TEnum>(stringValue, true, out var enumValue))
                    {
                        list.Add(enumValue);
                    }
                }
                else if (reader.TokenType == JsonTokenType.Number)
                {
                    var intValue = reader.GetInt32();
                    list.Add((TEnum)(object)intValue);
                }
            }

            throw new JsonException("Unexpected end of array");
        }

        public override void Write(Utf8JsonWriter writer, TEnum[] value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var item in value)
            {
                writer.WriteStringValue(item.ToString());
            }
            writer.WriteEndArray();
        }
    }
}
