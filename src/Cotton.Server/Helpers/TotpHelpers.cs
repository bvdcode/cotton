using Cotton.Server.Models;
using OtpNet;

namespace Cotton.Server.Helpers
{
    public class TotpHelpers
    {
        public static TotpSetup CreateSetup(string issuer, string accountName)
        {
            var secretBytes = KeyGeneration.GenerateRandomKey(20); // 160-bit
            var secretBase32 = Base32Encoding.ToString(secretBytes);
            var label = Uri.EscapeDataString(accountName);
            var issuerEsc = Uri.EscapeDataString(issuer);
            var uri = $"otpauth://totp/{label}?secret={secretBase32}&issuer={issuerEsc}&digits=6&period=30";
            return new TotpSetup
            {
                SecretBase32 = secretBase32,
                OtpAuthUri = uri
            };
        }

        public static bool VerifyCode(string secretBase32, string code)
        {
            var secretBytes = Base32Encoding.ToBytes(secretBase32);
            var totp = new Totp(secretBytes, step: 30, totpSize: 6);
            return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
        }
    }
}
