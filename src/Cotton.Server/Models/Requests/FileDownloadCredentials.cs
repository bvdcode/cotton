// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Requests
{
    public class FileDownloadCredentials
    {
        public string? Token { get; set; }
        public string? DownloadToken { get; set; }

        public string Value => DownloadToken ?? Token ?? string.Empty;

        public bool IsValid => ((Token is null) != (DownloadToken is null))
            && !string.IsNullOrWhiteSpace(Value);

        public string ToQueryString() =>
            $"{(DownloadToken is not null ? "downloadToken" : "token")}={Uri.EscapeDataString(Value)}";
    }
}
