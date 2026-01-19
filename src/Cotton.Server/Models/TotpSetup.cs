namespace Cotton.Server.Models
{
    public class TotpSetup
    {
        public string SecretBase32 { get; set; } = null!;
        public string OtpAuthUri { get; set; } = null!;
    }
}
