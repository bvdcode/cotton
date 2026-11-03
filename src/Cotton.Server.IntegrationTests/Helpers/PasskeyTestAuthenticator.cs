// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Buffers.Binary;
using System.Formats.Cbor;
using Microsoft.AspNetCore.WebUtilities;

namespace Cotton.Server.IntegrationTests.Helpers
{
    internal class PasskeyTestAuthenticator : IDisposable
    {
        public const string Origin = "http://localhost";
        private readonly ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        public byte[] CredentialId { get; } = RandomNumberGenerator.GetBytes(32);
        public byte[] PublicKey => EncodePublicKey();

        public PasskeyAttestationCredentialDto Register(JsonElement options)
        {
            byte[] clientData = ClientData(options, "webauthn.create", Origin);
            byte[] data = AuthenticatorData(options.GetProperty("rp").GetProperty("id").GetString()!, registration: true, 0);
            CborWriter attestation = new();
            attestation.WriteStartMap(3);
            attestation.WriteTextString("fmt");
            attestation.WriteTextString("none");
            attestation.WriteTextString("authData");
            attestation.WriteByteString(data);
            attestation.WriteTextString("attStmt");
            attestation.WriteStartMap(0);
            attestation.WriteEndMap();
            attestation.WriteEndMap();
            return new PasskeyAttestationCredentialDto
            {
                Id = WebEncoders.Base64UrlEncode(CredentialId),
                RawId = WebEncoders.Base64UrlEncode(CredentialId),
                Type = "public-key",
                Transports = ["internal", "hybrid"],
                Response = new PasskeyAttestationResponseDto
                {
                    ClientDataJson = WebEncoders.Base64UrlEncode(clientData),
                    AttestationObject = WebEncoders.Base64UrlEncode(attestation.Encode()),
                },
            };
        }

        public PasskeyAssertionCredentialDto Assert(JsonElement options, byte[]? userHandle, uint counter = 1, string origin = Origin)
        {
            byte[] clientData = ClientData(options, "webauthn.get", origin);
            byte[] data = AuthenticatorData(options.GetProperty("rpId").GetString()!, registration: false, counter);
            byte[] signature = key.SignData([.. data, .. SHA256.HashData(clientData)], HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence);
            return new PasskeyAssertionCredentialDto
            {
                Id = WebEncoders.Base64UrlEncode(CredentialId),
                RawId = WebEncoders.Base64UrlEncode(CredentialId),
                Type = "public-key",
                Response = new PasskeyAssertionResponseDto
                {
                    ClientDataJson = WebEncoders.Base64UrlEncode(clientData),
                    AuthenticatorData = WebEncoders.Base64UrlEncode(data),
                    Signature = WebEncoders.Base64UrlEncode(signature),
                    UserHandle = userHandle is null ? null : WebEncoders.Base64UrlEncode(userHandle),
                },
            };
        }

        private static byte[] ClientData(JsonElement options, string operation, string origin)
        {
            return JsonSerializer.SerializeToUtf8Bytes(new
            {
                type = operation,
                challenge = options.GetProperty("challenge").GetString(),
                origin,
                crossOrigin = false,
            });
        }

        private byte[] AuthenticatorData(string relyingPartyId, bool registration, uint counter)
        {
            byte[] header = new byte[37];
            SHA256.HashData(Encoding.UTF8.GetBytes(relyingPartyId)).CopyTo(header, 0);
            header[32] = registration ? (byte)0x45 : (byte)0x05;
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(33), counter);
            if (!registration)
            {
                return header;
            }
            byte[] attested = new byte[18];
            BinaryPrimitives.WriteUInt16BigEndian(attested.AsSpan(16), (ushort)CredentialId.Length);
            return [.. header, .. attested, .. CredentialId, .. PublicKey];
        }

        private byte[] EncodePublicKey()
        {
            ECParameters parameters = key.ExportParameters(false);
            CborWriter writer = new();
            writer.WriteStartMap(5);
            writer.WriteInt32(1);
            writer.WriteInt32(2);
            writer.WriteInt32(3);
            writer.WriteInt32(-7);
            writer.WriteInt32(-1);
            writer.WriteInt32(1);
            writer.WriteInt32(-2);
            writer.WriteByteString(parameters.Q.X!);
            writer.WriteInt32(-3);
            writer.WriteByteString(parameters.Q.Y!);
            writer.WriteEndMap();
            return writer.Encode();
        }

        public void Dispose() => key.Dispose();
    }
}
