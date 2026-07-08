// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.Passkeys
{
    /// <summary>
    /// Resolves WebAuthn authenticator metadata from the AAGUID reported during registration.
    /// </summary>
    public static class PasskeyAuthenticatorResolver
    {
        private static readonly string[] SecurityKeyTransports = ["ble", "nfc", "smart-card", "usb"];
        private static readonly string[] DeviceTransports = ["hybrid", "internal"];

        private static readonly IReadOnlyDictionary<Guid, string> KnownAuthenticators =
            new Dictionary<Guid, string>
            {
                [Guid.Parse("ea9b8d66-4d01-1d21-3ce4-b6b48cb575d4")] = "Google Password Manager",
                [Guid.Parse("08987058-cadc-4b81-b6e1-30de50dcbe96")] = "Windows Hello",
                [Guid.Parse("9ddd1817-af5a-4672-a2b9-3e3dd95000a9")] = "Windows Hello",
                [Guid.Parse("6028b017-b1d4-4c02-b4b3-afcdafc96bb2")] = "Windows Hello",
                [Guid.Parse("d3452668-01fd-4c12-926c-83a4204853aa")] = "Microsoft Password Manager",
                [Guid.Parse("fbfc3007-154e-4ecc-8c0b-6e020557d7bd")] = "Apple Passwords",
                [Guid.Parse("dd4ec289-e01d-41c9-bb89-70fa845d4bf2")] = "iCloud Keychain",
                [Guid.Parse("53414d53-554e-4700-0000-000000000000")] = "Samsung Pass",
                [Guid.Parse("bada5566-a7aa-401f-bd96-45619a55120d")] = "1Password",
                [Guid.Parse("d548826e-79b4-db40-a3d8-11116f7e8349")] = "Bitwarden",
                [Guid.Parse("531126d6-e717-415c-9320-3d9aa6981239")] = "Dashlane",
                [Guid.Parse("0ea242b4-43c4-4a1b-8b17-dd6d0b6baec6")] = "Keeper",
                [Guid.Parse("b84e4048-15dc-4dd0-8640-f4f60813c8af")] = "NordPass",
                [Guid.Parse("f3809540-7f14-49c1-a8b3-8f813b225541")] = "Enpass",
                [Guid.Parse("50726f74-6f6e-5061-7373-50726f746f6e")] = "Proton Pass",
                [Guid.Parse("fdb141b2-5d84-443e-8a35-4698c205a502")] = "KeePassXC",
                [Guid.Parse("eaecdef2-1c31-5634-8639-f1cbd9c00a08")] = "KeePassDX",
                [Guid.Parse("9addb28c-b46f-4402-808f-019651441ff3")] = "KeePassPasskey",
                [Guid.Parse("b7d3f68e-88a6-471e-9ecf-2df26d041ede")] = "Security Key NFC by Yubico",
                [Guid.Parse("a4e9fc6d-4cbe-4758-b8ba-37598bb5bbaa")] = "Security Key NFC by Yubico",
                [Guid.Parse("e77e3c64-05e3-428b-8824-0cbeb04b829d")] = "Security Key NFC by Yubico",
                [Guid.Parse("f8a011f3-8c0a-4d15-8006-17111f9edc7d")] = "Security Key by Yubico",
                [Guid.Parse("b92c3f9a-c014-4056-887f-140a2501163b")] = "Security Key by Yubico",
                [Guid.Parse("149a2021-8ef6-4133-96b8-81f8d5b7f1f5")] = "Security Key by Yubico with NFC",
                [Guid.Parse("6d44ba9b-f6ec-2e49-b930-0c8fe920cb73")] = "Security Key by Yubico with NFC",
                [Guid.Parse("fa2b99dc-9e39-4257-8f92-4a30d23c4118")] = "YubiKey 5 Series with NFC",
                [Guid.Parse("2fc0579f-8113-47ea-b116-bb5a8db9202a")] = "YubiKey 5 Series with NFC",
                [Guid.Parse("f4ce5fc0-57d3-46f5-a736-efb7d5bc63b5")] = "YubiKey 5 Series with NFC",
                [Guid.Parse("d7781e5d-e353-46aa-afe2-3ca49f13332a")] = "YubiKey 5 Series with NFC",
                [Guid.Parse("0a357157-9b18-4c8a-920e-d156e972b2f8")] = "YubiKey 5 Series",
                [Guid.Parse("19083c3d-8383-4b18-bc03-8f1c9ab2fd1b")] = "YubiKey 5 Series",
                [Guid.Parse("cb69481e-8ff7-4039-93ec-0a2729a154a8")] = "YubiKey 5 Series",
                [Guid.Parse("ee882879-721c-4913-9775-3dfcce97072a")] = "YubiKey 5 Series",
                [Guid.Parse("ff4dac45-ede8-4ec2-aced-cf66103f4335")] = "YubiKey 5 Series",
                [Guid.Parse("c5ef55ff-ad9a-4b9f-b580-adebafe026d0")] = "YubiKey 5 Series with Lightning",
                [Guid.Parse("a02167b9-ae71-4ac7-9a07-06432ebb6f1c")] = "YubiKey 5 Series with Lightning",
                [Guid.Parse("03012cb7-4fb2-42e7-9e8d-a81f10e2a5e9")] = "YubiKey 5 Series with Lightning",
                [Guid.Parse("90636e1f-ef82-43bf-bdcf-5255f139d12f")] = "YubiKey Bio Series",
                [Guid.Parse("34744913-4f57-4e6e-a527-e9ec3c4b94e6")] = "YubiKey Bio Series",
                [Guid.Parse("d8522d9f-575b-4866-88a9-ba99fa02f35b")] = "YubiKey Bio Series",
                [Guid.Parse("42b4fb4a-2866-43b2-9bf7-6c6669c2e5d3")] = "Google Titan Security Key v2"
            };

        /// <summary>
        /// Resolves a friendly authenticator name for display.
        /// </summary>
        public static string? ResolveName(Guid aaGuid)
        {
            return aaGuid != Guid.Empty && KnownAuthenticators.TryGetValue(aaGuid, out string? name)
                ? name
                : null;
        }

        /// <summary>
        /// Resolves the best default credential name when the user did not provide a label.
        /// </summary>
        public static string ResolveDefaultName(Guid aaGuid, IEnumerable<string> transports)
        {
            string? authenticatorName = ResolveName(aaGuid);
            if (!string.IsNullOrWhiteSpace(authenticatorName))
            {
                return authenticatorName;
            }

            HashSet<string> normalizedTransports = transports
                .Select(x => x.Trim().ToLowerInvariant())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (normalizedTransports.Overlaps(SecurityKeyTransports))
            {
                return "Security key";
            }

            if (normalizedTransports.Overlaps(DeviceTransports))
            {
                return "Device passkey";
            }

            return "Passkey";
        }
    }
}
