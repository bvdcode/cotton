// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Fido2NetLib;

namespace Cotton.Server.Models.Dto
{
    public class PasskeyRegistrationOptionsResponseDto
    {
        public string RequestId { get; set; } = null!;

        public CredentialCreateOptions Options { get; set; } = null!;
    }
}
