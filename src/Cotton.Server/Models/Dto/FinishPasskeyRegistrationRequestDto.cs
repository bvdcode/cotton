// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    public class FinishPasskeyRegistrationRequestDto
    {
        public string RequestId { get; set; } = null!;

        public string? Label { get; set; }

        public PasskeyAttestationCredentialDto Credential { get; set; } = null!;
    }
}
