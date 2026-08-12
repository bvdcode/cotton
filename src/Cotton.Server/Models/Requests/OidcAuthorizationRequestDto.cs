// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Requests
{
    public class OidcAuthorizationRequestDto
    {
        public string? ReturnUrl { get; set; }

        public bool TrustDevice { get; set; }
    }
}
