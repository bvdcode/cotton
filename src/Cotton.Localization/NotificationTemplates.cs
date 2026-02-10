namespace Cotton.Localization
{
    public static class NotificationTemplates
    {
        public static string FailedLoginAttemptTitle => "Failed login attempt";

        public static string FailedLoginAttemptContent(
            string username,
            string ipAddress,
            string userAgent,
            string device,
            string country,
            string region,
            string city)
        {
            return $"Failed login attempt for '{username}'.\n" +
                   $"IP: {ipAddress}\n" +
                   $"User-Agent: {userAgent}\n" +
                   $"Device: {device}\n" +
                   $"Location: {city}, {region}, {country}";
        }

        public static string SuccessfulLoginTitle => "Successful login";

        public static string SuccessfulLoginContent(
            string ipAddress,
            string userAgent,
            string device,
            string country,
            string region,
            string city)
        {
            return $"New successful login.\n" +
                   $"IP: {ipAddress}\n" +
                   $"User-Agent: {userAgent}\n" +
                   $"Device: {device}\n" +
                   $"Location: {city}, {region}, {country}";
        }

        public static string OtpEnabledTitle => "Two-factor authentication enabled";

        public static string OtpEnabledContent(
            string ipAddress,
            string userAgent,
            string device,
            string country,
            string region,
            string city)
        {
            return $"Two-factor authentication (TOTP) was enabled.\n" +
                   $"IP: {ipAddress}\n" +
                   $"User-Agent: {userAgent}\n" +
                   $"Device: {device}\n" +
                   $"Location: {city}, {region}, {country}";
        }

        public static string TotpFailedAttemptTitle => "Invalid two-factor authentication code";

        public static string TotpFailedAttemptContent(
            int failedAttempts,
            string ipAddress,
            string userAgent,
            string device,
            string country,
            string region,
            string city)
        {
            return $"Invalid TOTP code entered ({failedAttempts} failed attempt(s)).\n" +
                   $"IP: {ipAddress}\n" +
                   $"User-Agent: {userAgent}\n" +
                   $"Device: {device}\n" +
                   $"Location: {city}, {region}, {country}";
        }

        public static string TotpLockoutTitle => "Two-factor authentication locked";

        public static string TotpLockoutContent(
            int maxFailedAttempts,
            string ipAddress,
            string userAgent,
            string device,
            string country,
            string region,
            string city)
        {
            return $"Two-factor authentication was locked after {maxFailedAttempts} failed attempt(s).\n" +
                   $"IP: {ipAddress}\n" +
                   $"User-Agent: {userAgent}\n" +
                   $"Device: {device}\n" +
                   $"Location: {city}, {region}, {country}";
        }

        public static string WebDavTokenResetTitle => "WebDAV token reset";

        public static string WebDavTokenResetContent(
            string ipAddress,
            string userAgent,
            string device,
            string country,
            string region,
            string city)
        {
            return $"WebDAV token was reset.\n" +
                   $"IP: {ipAddress}\n" +
                   $"User-Agent: {userAgent}\n" +
                   $"Device: {device}\n" +
                   $"Location: {city}, {region}, {country}";
        }
    }
}
