// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.ContentTypes;
using Cotton.Previews;

namespace Cotton.Server.Services
{
    public static class UploadContentTypeResolver
    {
        public static string Resolve(string? fileName, string? contentType)
        {
            string resolved = FileContentTypeResolver.Resolve(fileName, contentType);
            if (FileContentTypeResolver.IsSourceTextFileName(fileName)
                && PreviewGeneratorProvider.GetGeneratorByContentType(resolved) is null
                && (resolved.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
                    || resolved.StartsWith("application/x-", StringComparison.OrdinalIgnoreCase)))
            {
                return "text/plain";
            }

            return resolved;
        }
    }
}
