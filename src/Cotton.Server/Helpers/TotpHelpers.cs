// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models;
using OtpNet;

namespace Cotton.Server.Helpers
{
    public class TotpHelpers
    {
        public static TotpSetup CreateSetup(
            string issuer,
            string accountName,
            Uri imageUri)
        {
            ArgumentNullException.ThrowIfNull(imageUri);

            byte[] secretBytes = KeyGeneration.GenerateRandomKey(20); // 160-bit
            string secretBase32 = Base32Encoding.ToString(secretBytes);
            string issuerEsc = Uri.EscapeDataString(issuer);
            string accountEsc = Uri.EscapeDataString(accountName);
            string imageUriEsc = Uri.EscapeDataString(imageUri.AbsoluteUri);
            string uri =
                $"otpauth://totp/{issuerEsc}:{accountEsc}" +
                $"?secret={secretBase32}&issuer={issuerEsc}" +
                $"&digits=6&period=30&imagelink={imageUriEsc}";
            return new TotpSetup
            {
                SecretBase32 = secretBase32,
                OtpAuthUri = uri
            };
        }

        public static bool VerifyCode(string secretBase32, string code)
        {
            byte[] secretBytes = Base32Encoding.ToBytes(secretBase32);
            Totp totp = new Totp(secretBytes, step: 30, totpSize: 6);
            return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
        }
    }
}
