// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Fido2NetLib;

namespace Cotton.Server.Models.Dto
{
    public class PasskeyAssertionCredentialDto
    {
        public string Id { get; set; } = null!;

        public string RawId { get; set; } = null!;

        public string Type { get; set; } = null!;

        public PasskeyAssertionResponseDto Response { get; set; } = null!;
    }
}
