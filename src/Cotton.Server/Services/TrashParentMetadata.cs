// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Text.Json;

namespace Cotton.Server.Services
{
    public static class TrashParentMetadata
    {
        public const string Key = "originalParentsV1";
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public static List<TrashParent>? Read(Dictionary<string, string>? metadata)
        {
            return metadata is not null && metadata.TryGetValue(Key, out string? value)
                ? JsonSerializer.Deserialize<List<TrashParent>>(value, JsonOptions)
                : null;
        }

        public static Dictionary<string, string> Write(
            Dictionary<string, string>? metadata, IReadOnlyList<TrashParent> parents)
        {
            Dictionary<string, string> result = TrashRestoreCoordinator.SetOriginalParentPath(
                metadata, string.Join(Constants.DefaultPathSeparator, parents.Skip(1).Select(x => x.Name)));
            result[Key] = JsonSerializer.Serialize(parents, JsonOptions);
            return result;
        }

        public static Dictionary<string, string>? CopyFolderMetadata(Dictionary<string, string>? metadata)
        {
            if (metadata is null)
            {
                return null;
            }
            Dictionary<string, string> result = new(metadata);
            result.Remove(Key);
            result.Remove(TrashMetadataKeys.OriginalParentPath);
            return result;
        }

        public static Dictionary<string, string> ReplaceParent(
            Dictionary<string, string>? metadata, IReadOnlyList<TrashParent> parents, TrashParent replacement)
        {
            if (replacement.Id == Guid.Empty || parents.Count(x => x.Id == replacement.Id) != 1)
            {
                throw new ArgumentException("The saved path must contain exactly one matching parent.", nameof(parents));
            }
            return Write(metadata, parents.Select(parent => parent.Id == replacement.Id ? replacement : parent).ToArray());
        }
    }
}
