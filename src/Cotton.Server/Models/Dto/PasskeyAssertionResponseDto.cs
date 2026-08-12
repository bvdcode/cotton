// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Fido2NetLib;

namespace Cotton.Server.Models.Dto
{
    public class PasskeyAssertionResponseDto
    {
        public string AuthenticatorData { get; set; } = null!;

        public string ClientDataJson { get; set; } = null!;

        public string Signature { get; set; } = null!;

        public string? UserHandle { get; set; }
    }
}
