// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Fido2NetLib;

namespace Cotton.Server.Models.Dto
{
    public class FinishPasskeyAssertionRequestDto
    {
        public string RequestId { get; set; } = null!;

        public bool TrustDevice { get; set; }

        public PasskeyAssertionCredentialDto Credential { get; set; } = null!;
    }
}
