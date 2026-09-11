// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.WebUtilities;

namespace Cotton.Server.Services.Passkeys
{
    public static class PasskeyProtocolMapper
    {
        public static PasskeyCredentialDto ToDto(UserPasskeyCredential credential)
        {
            return new PasskeyCredentialDto
            {
                Id = credential.Id,
                Label = credential.Label,
                CredentialId = WebEncoders.Base64UrlEncode(credential.CredentialId),
                Transports = credential.Transports,
                AaGuid = credential.AaGuid,
                AuthenticatorName = PasskeyAuthenticatorResolver.ResolveName(credential.AaGuid),
                AuthenticatorKind = PasskeyAuthenticatorResolver.ResolveKind(credential.Transports),
                IsBackupEligible = credential.IsBackupEligible,
                IsBackedUp = credential.IsBackedUp,
                CreatedAt = credential.CreatedAt,
                LastUsedAt = credential.LastUsedAt,
            };
        }

        public static string GetDisplayName(User user)
        {
            string displayName = $"{user.FirstName} {user.LastName}".Trim();
            return string.IsNullOrWhiteSpace(displayName) ? user.Username : displayName;
        }

        public static string GetAuditName(UserPasskeyCredential credential)
        {
            return credential.Label
                ?? PasskeyAuthenticatorResolver.ResolveDisplayName(credential.AaGuid, credential.Transports);
        }

        public static AuthenticatorAttestationRawResponse ToAttestationResponse(
            PasskeyAttestationCredentialDto credential)
        {
            return new AuthenticatorAttestationRawResponse
            {
                Id = credential.Id,
                RawId = DecodeBrowserBuffer(credential.RawId),
                Type = PublicKeyCredentialType.PublicKey,
                Response = new()
                {
                    AttestationObject = DecodeBrowserBuffer(credential.Response.AttestationObject),
                    ClientDataJson = DecodeBrowserBuffer(credential.Response.ClientDataJson),
                    Transports = ParseTransports(credential.Transports),
                },
            };
        }

        public static AuthenticatorAssertionRawResponse ToAssertionResponse(
            PasskeyAssertionCredentialDto credential)
        {
            return new AuthenticatorAssertionRawResponse
            {
                Id = credential.Id,
                RawId = DecodeBrowserBuffer(credential.RawId),
                Type = PublicKeyCredentialType.PublicKey,
                Response = new()
                {
                    AuthenticatorData = DecodeBrowserBuffer(credential.Response.AuthenticatorData),
                    ClientDataJson = DecodeBrowserBuffer(credential.Response.ClientDataJson),
                    Signature = DecodeBrowserBuffer(credential.Response.Signature),
                    UserHandle = string.IsNullOrEmpty(credential.Response.UserHandle)
                        ? []
                        : DecodeBrowserBuffer(credential.Response.UserHandle),
                },
            };
        }

        public static PublicKeyCredentialDescriptor CreateCredentialDescriptor(
            byte[] credentialId,
            string[] transports)
        {
            AuthenticatorTransport[] parsedTransports = ParseTransports(transports);
            return parsedTransports.Length == 0
                ? new PublicKeyCredentialDescriptor(credentialId)
                : new PublicKeyCredentialDescriptor(
                    PublicKeyCredentialType.PublicKey,
                    credentialId,
                    parsedTransports);
        }

        public static string[] NormalizeTransports(IEnumerable<AuthenticatorTransport>? transports)
        {
            return transports?
                .Select(transport => transport.ToString().ToLowerInvariant())
                .Distinct()
                .Order()
                .ToArray() ?? [];
        }

        public static byte[] DecodeBrowserBuffer(string value)
        {
            return WebEncoders.Base64UrlDecode(value);
        }

        public static uint ToSignatureCounter(long value)
        {
            if (value <= 0)
            {
                return 0;
            }

            return value >= uint.MaxValue ? uint.MaxValue : (uint)value;
        }

        private static AuthenticatorTransport[] ParseTransports(IEnumerable<string>? transports)
        {
            if (transports is null)
            {
                return [];
            }

            return transports
                .Select(value => Enum.TryParse(value, ignoreCase: true, out AuthenticatorTransport transport)
                    ? transport
                    : (AuthenticatorTransport?)null)
                .Where(transport => transport.HasValue)
                .Select(transport => transport!.Value)
                .Distinct()
                .ToArray();
        }
    }
}
