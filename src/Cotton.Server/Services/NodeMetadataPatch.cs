// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using EasyExtensions.AspNetCore.Exceptions;

namespace Cotton.Server.Services
{
    public static class NodeMetadataPatch
    {
        public static Dictionary<string, string>? Apply(
            Dictionary<string, string>? source,
            IReadOnlyDictionary<string, string?>? patch)
        {
            if (patch is null)
            {
                return source;
            }
            if (patch.Keys.Any(string.IsNullOrWhiteSpace))
            {
                throw new BadRequestException<Node>("Metadata keys must be non-empty strings.");
            }
            Dictionary<string, string> result = source is null ? [] : new(source);
            foreach ((string key, string? value) in patch)
            {
                if (value is null)
                {
                    result.Remove(key);
                }
                else
                {
                    result[key] = value;
                }
            }
            return result;
        }
    }
}
