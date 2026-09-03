// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Fido2NetLib;
using Cotton.Server.Models.Enums;
using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    public class PasskeyCredentialDto : BaseDto<Guid>
    {
        public string? Label { get; set; }

        public string CredentialId { get; set; } = null!;

        public string[] Transports { get; set; } = [];

        public Guid AaGuid { get; set; }

        public string? AuthenticatorName { get; set; }

        public PasskeyAuthenticatorKind AuthenticatorKind { get; set; }

        public bool IsBackupEligible { get; set; }

        public bool IsBackedUp { get; set; }

        public DateTime? LastUsedAt { get; set; }
    }
}
