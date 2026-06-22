// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Globalization;
using System.Text;
using System.Xml;

namespace Cotton.Server.Services.WebDav
{
    /// <summary>
    /// Represents a WebDAV resource (file or collection)
    /// </summary>
    public record WebDavResource(
        string Href,
        string DisplayName,
        bool IsCollection,
        long ContentLength,
        DateTimeOffset LastModified,
        string ETag,
        string? ContentType = null,
        WebDavQuota? Quota = null);
}
