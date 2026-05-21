// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Fido2NetLib;

namespace Cotton.Server.Models.Dto
{
    public class PasskeyCredentialDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string CredentialId { get; set; } = null!;
        public string[] Transports { get; set; } = [];
        public bool IsBackupEligible { get; set; }
        public bool IsBackedUp { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
    }

    public class PasskeyRegistrationOptionsResponseDto
    {
        public string RequestId { get; set; } = null!;
        public CredentialCreateOptions Options { get; set; } = null!;
    }

    public class PasskeyAssertionOptionsResponseDto
    {
        public string RequestId { get; set; } = null!;
        public AssertionOptions Options { get; set; } = null!;
    }

    public class BeginPasskeyRegistrationRequestDto
    {
        public string? Name { get; set; }
    }

    public class FinishPasskeyRegistrationRequestDto
    {
        public string RequestId { get; set; } = null!;
        public string? Name { get; set; }
        public PasskeyAttestationCredentialDto Credential { get; set; } = null!;
    }

    public class BeginPasskeyAssertionRequestDto
    {
        public string? Username { get; set; }
    }

    public class RenamePasskeyRequestDto
    {
        public string Name { get; set; } = null!;
    }

    public class FinishPasskeyAssertionRequestDto
    {
        public string RequestId { get; set; } = null!;
        public bool TrustDevice { get; set; }
        public PasskeyAssertionCredentialDto Credential { get; set; } = null!;
    }

    public class PasskeyAttestationCredentialDto
    {
        public string Id { get; set; } = null!;
        public string RawId { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string[] Transports { get; set; } = [];
        public PasskeyAttestationResponseDto Response { get; set; } = null!;
    }

    public class PasskeyAttestationResponseDto
    {
        public string AttestationObject { get; set; } = null!;
        public string ClientDataJson { get; set; } = null!;
    }

    public class PasskeyAssertionCredentialDto
    {
        public string Id { get; set; } = null!;
        public string RawId { get; set; } = null!;
        public string Type { get; set; } = null!;
        public PasskeyAssertionResponseDto Response { get; set; } = null!;
    }

    public class PasskeyAssertionResponseDto
    {
        public string AuthenticatorData { get; set; } = null!;
        public string ClientDataJson { get; set; } = null!;
        public string Signature { get; set; } = null!;
        public string? UserHandle { get; set; }
    }
}
