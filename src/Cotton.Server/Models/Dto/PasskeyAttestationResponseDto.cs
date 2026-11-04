// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    public class PasskeyAttestationResponseDto
    {
        public string AttestationObject { get; set; } = null!;

        public string ClientDataJson { get; set; } = null!;
    }
}
