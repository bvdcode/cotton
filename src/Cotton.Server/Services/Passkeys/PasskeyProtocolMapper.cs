// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using EasyExtensions.Passkeys.Models;
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

        public static RegistrationResponse ToAttestationResponse(
            PasskeyAttestationCredentialDto credential)
        {
            return new RegistrationResponse(credential.Id, DecodeBrowserBuffer(credential.RawId), credential.Type,
                new AttestationResponse(DecodeBrowserBuffer(credential.Response.ClientDataJson),
                    DecodeBrowserBuffer(credential.Response.AttestationObject))
                {
                    Transports = ParseTransports(credential.Transports),
                });
        }

        public static AuthenticationResponse ToAssertionResponse(PasskeyAssertionCredentialDto credential)
        {
            return new AuthenticationResponse(credential.Id, DecodeBrowserBuffer(credential.RawId), credential.Type,
                new AssertionResponse(DecodeBrowserBuffer(credential.Response.ClientDataJson),
                    DecodeBrowserBuffer(credential.Response.AuthenticatorData), DecodeBrowserBuffer(credential.Response.Signature))
                {
                    UserHandle = credential.Response.UserHandle is null
                        ? null
                        : DecodeBrowserBuffer(credential.Response.UserHandle),
                });
        }

        public static CredentialDescriptor CreateCredentialDescriptor(byte[] credentialId, string[] transports)
        {
            return new CredentialDescriptor(credentialId)
            {
                Transports = ParseTransports(transports),
            };
        }

        public static string[] NormalizeTransports(IEnumerable<AuthenticatorTransport>? transports)
        {
            return transports?
                .Select(FormatTransport)
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
                .Select(value => value?.ToLowerInvariant() switch
                {
                    "usb" => AuthenticatorTransport.Usb,
                    "nfc" => AuthenticatorTransport.Nfc,
                    "ble" => AuthenticatorTransport.Ble,
                    "internal" => AuthenticatorTransport.Internal,
                    "hybrid" => AuthenticatorTransport.Hybrid,
                    "smart-card" => AuthenticatorTransport.SmartCard,
                    _ => (AuthenticatorTransport?)null,
                })
                .Where(transport => transport.HasValue)
                .Select(transport => transport!.Value)
                .Distinct()
                .ToArray();
        }

        private static string FormatTransport(AuthenticatorTransport transport)
        {
            return transport switch
            {
                AuthenticatorTransport.Usb => "usb",
                AuthenticatorTransport.Nfc => "nfc",
                AuthenticatorTransport.Ble => "ble",
                AuthenticatorTransport.Internal => "internal",
                AuthenticatorTransport.Hybrid => "hybrid",
                AuthenticatorTransport.SmartCard => "smart-card",
                _ => throw new ArgumentOutOfRangeException(nameof(transport)),
            };
        }
    }
}
