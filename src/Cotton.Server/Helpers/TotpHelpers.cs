using OtpNet;

namespace Cotton.Server.Helpers
{
    public class TotpHelpers
    {
        public static (string secretBase32, string otpauthUri) CreateSetup(string issuer, string accountName)
        {
            var secretBytes = KeyGeneration.GenerateRandomKey(20); // 160-bit
            var secretBase32 = Base32Encoding.ToString(secretBytes);
            var label = Uri.EscapeDataString($"{issuer}:{accountName}");
            var issuerEsc = Uri.EscapeDataString(issuer);
            var uri = $"otpauth://totp/{label}?secret={secretBase32}&issuer={issuerEsc}&digits=6&period=30";
            return (secretBase32, uri);
        }

        public static bool VerifyCode(string secretBase32, string code)
        {
            var secretBytes = Base32Encoding.ToBytes(secretBase32);
            var totp = new Totp(secretBytes, step: 30, totpSize: 6);
            return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
        }
    }
}
