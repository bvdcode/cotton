// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Fido2NetLib;

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the passkey credential API payload.
    /// </summary>
    public class PasskeyCredentialDto
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        public Guid Id { get; set; }
        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        public string Name { get; set; } = null!;
        /// <summary>
        /// Gets or sets the WebAuthn credential identifier encoded for transport.
        /// </summary>
        public string CredentialId { get; set; } = null!;
        /// <summary>
        /// Gets or sets the authenticator transports reported by the browser.
        /// </summary>
        public string[] Transports { get; set; } = [];
        /// <summary>
        /// Indicates whether the passkey can be backed up by the authenticator provider.
        /// </summary>
        public bool IsBackupEligible { get; set; }
        /// <summary>
        /// Indicates whether the passkey is currently backed up by the authenticator provider.
        /// </summary>
        public bool IsBackedUp { get; set; }
        /// <summary>
        /// Gets or sets the timestamp when the resource was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }
        /// <summary>
        /// Gets or sets the timestamp when the credential was last used.
        /// </summary>
        public DateTime? LastUsedAt { get; set; }
    }

    /// <summary>
    /// Represents the passkey registration options response API payload.
    /// </summary>
    public class PasskeyRegistrationOptionsResponseDto
    {
        /// <summary>
        /// Gets or sets the server-issued passkey ceremony request identifier.
        /// </summary>
        public string RequestId { get; set; } = null!;
        /// <summary>
        /// Gets or sets the WebAuthn options returned to the browser.
        /// </summary>
        public CredentialCreateOptions Options { get; set; } = null!;
    }

    /// <summary>
    /// Represents the passkey assertion options response API payload.
    /// </summary>
    public class PasskeyAssertionOptionsResponseDto
    {
        /// <summary>
        /// Gets or sets the server-issued passkey ceremony request identifier.
        /// </summary>
        public string RequestId { get; set; } = null!;
        /// <summary>
        /// Gets or sets the WebAuthn options returned to the browser.
        /// </summary>
        public AssertionOptions Options { get; set; } = null!;
    }

    /// <summary>
    /// Represents the begin passkey registration request payload accepted by the API.
    /// </summary>
    public class BeginPasskeyRegistrationRequestDto
    {
        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        public string? Name { get; set; }
    }

    /// <summary>
    /// Represents the finish passkey registration request payload accepted by the API.
    /// </summary>
    public class FinishPasskeyRegistrationRequestDto
    {
        /// <summary>
        /// Gets or sets the server-issued passkey ceremony request identifier.
        /// </summary>
        public string RequestId { get; set; } = null!;
        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        public string? Name { get; set; }
        /// <summary>
        /// Gets or sets the WebAuthn credential payload returned by the browser.
        /// </summary>
        public PasskeyAttestationCredentialDto Credential { get; set; } = null!;
    }

    /// <summary>
    /// Represents the begin passkey assertion request payload accepted by the API.
    /// </summary>
    public class BeginPasskeyAssertionRequestDto
    {
        /// <summary>
        /// Gets or sets the normalized username.
        /// </summary>
        public string? Username { get; set; }
    }

    /// <summary>
    /// Represents the rename passkey request payload accepted by the API.
    /// </summary>
    public class RenamePasskeyRequestDto
    {
        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        public string Name { get; set; } = null!;
    }

    /// <summary>
    /// Represents the finish passkey assertion request payload accepted by the API.
    /// </summary>
    public class FinishPasskeyAssertionRequestDto
    {
        /// <summary>
        /// Gets or sets the server-issued passkey ceremony request identifier.
        /// </summary>
        public string RequestId { get; set; } = null!;
        /// <summary>
        /// Gets or sets trust device.
        /// </summary>
        public bool TrustDevice { get; set; }
        /// <summary>
        /// Gets or sets the WebAuthn credential payload returned by the browser.
        /// </summary>
        public PasskeyAssertionCredentialDto Credential { get; set; } = null!;
    }

    /// <summary>
    /// Represents the passkey attestation credential API payload.
    /// </summary>
    public class PasskeyAttestationCredentialDto
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        public string Id { get; set; } = null!;
        /// <summary>
        /// Gets or sets raw id.
        /// </summary>
        public string RawId { get; set; } = null!;
        /// <summary>
        /// Gets or sets type.
        /// </summary>
        public string Type { get; set; } = null!;
        /// <summary>
        /// Gets or sets the authenticator transports reported by the browser.
        /// </summary>
        public string[] Transports { get; set; } = [];
        /// <summary>
        /// Gets or sets response.
        /// </summary>
        public PasskeyAttestationResponseDto Response { get; set; } = null!;
    }

    /// <summary>
    /// Represents the passkey attestation response API payload.
    /// </summary>
    public class PasskeyAttestationResponseDto
    {
        /// <summary>
        /// Gets or sets attestation object.
        /// </summary>
        public string AttestationObject { get; set; } = null!;
        /// <summary>
        /// Gets or sets client data json.
        /// </summary>
        public string ClientDataJson { get; set; } = null!;
    }

    /// <summary>
    /// Represents the passkey assertion credential API payload.
    /// </summary>
    public class PasskeyAssertionCredentialDto
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        public string Id { get; set; } = null!;
        /// <summary>
        /// Gets or sets raw id.
        /// </summary>
        public string RawId { get; set; } = null!;
        /// <summary>
        /// Gets or sets type.
        /// </summary>
        public string Type { get; set; } = null!;
        /// <summary>
        /// Gets or sets response.
        /// </summary>
        public PasskeyAssertionResponseDto Response { get; set; } = null!;
    }

    /// <summary>
    /// Represents the passkey assertion response API payload.
    /// </summary>
    public class PasskeyAssertionResponseDto
    {
        /// <summary>
        /// Gets or sets authenticator data.
        /// </summary>
        public string AuthenticatorData { get; set; } = null!;
        /// <summary>
        /// Gets or sets client data json.
        /// </summary>
        public string ClientDataJson { get; set; } = null!;
        /// <summary>
        /// Gets or sets signature.
        /// </summary>
        public string Signature { get; set; } = null!;
        /// <summary>
        /// Gets or sets user handle.
        /// </summary>
        public string? UserHandle { get; set; }
    }
}
