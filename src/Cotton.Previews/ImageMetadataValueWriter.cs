// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using MetadataExtractor;
using System.Collections;
using System.Globalization;

namespace Cotton.Previews
{
    internal static class ImageMetadataValueWriter
    {
        public static void Write(Dictionary<string, string> target, string key, object? value, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (value)
            {
                case null:
                    target[key] = "null";
                    break;
                case string text:
                    FlatMetadataWriter.WriteText(target, key, text);
                    break;
                case StringValue text:
                    FlatMetadataWriter.WriteText(target, key, text.ToString());
                    target[$"{key}.bytes"] = Convert.ToBase64String(text.Bytes);
                    break;
                case byte[] bytes:
                    target[key] = Convert.ToBase64String(bytes);
                    target[$"{key}.encoding"] = "base64";
                    break;
                case Rational rational:
                    target[key] = string.Create(CultureInfo.InvariantCulture, $"{rational.Numerator}/{rational.Denominator}");
                    break;
                case DateTime date:
                    target[key] = date.ToString("O", CultureInfo.InvariantCulture);
                    break;
                case bool boolean:
                    target[key] = boolean ? "true" : "false";
                    break;
                case IEnumerable items:
                    int index = 0;
                    foreach (object? item in items)
                    {
                        Write(target, $"{key}.{index.ToString(CultureInfo.InvariantCulture)}", item, cancellationToken);
                        index++;
                    }
                    target[$"{key}.length"] = index.ToString(CultureInfo.InvariantCulture);
                    break;
                case IFormattable formattable:
                    target[key] = formattable.ToString(null, CultureInfo.InvariantCulture);
                    break;
                default:
                    FlatMetadataWriter.WriteText(target, key, value.ToString()!);
                    break;
            }
        }
    }
}
