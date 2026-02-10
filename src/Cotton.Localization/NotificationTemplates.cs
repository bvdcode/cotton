namespace Cotton.Localization
{
    public static class NotificationTemplates
    {
        public static string FailedLoginAttemptTitle => "Failed login attempt";

        public static string FailedLoginAttemptContent(
            string username,
            string ipAddress,
            string device,
            string country,
            string region,
            string city)
        {
            return $"Someone tried to log in to your account '{username}' but failed. " +
                   $"The attempt was made from {device} in {city}, {region}, {country} ({ipAddress}).";
        }

        public static string SuccessfulLoginTitle => "New login to your account";

        public static string SuccessfulLoginContent(
            string ipAddress,
            string device,
            string country,
            string region,
            string city)
        {
            return $"Your account was accessed from {device} in {city}, {region}, {country} ({ipAddress}). " +
                   $"If this wasn't you, please secure your account immediately.";
        }

        public static string OtpEnabledTitle => "Two-factor authentication activated";

        public static string OtpEnabledContent(
            string ipAddress,
            string device,
            string country,
            string region,
            string city)
        {
            return $"Two-factor authentication has been enabled for your account from {device} " +
                   $"in {city}, {region}, {country} ({ipAddress}). " +
                   $"Your account is now more secure.";
        }

        public static string TotpFailedAttemptTitle => "Invalid authentication code";

        public static string TotpFailedAttemptContent(
            int failedAttempts,
            string ipAddress,
            string device,
            string country,
            string region,
            string city)
        {
            return $"An invalid two-factor authentication code was entered ({failedAttempts} failed attempt(s)). " +
                   $"The attempt was made from {device} in {city}, {region}, {country} ({ipAddress}). " +
                   $"If this wasn't you, your account may be under attack.";
        }

        public static string TotpLockoutTitle => "Account temporarily locked";

        public static string TotpLockoutContent(
            int maxFailedAttempts,
            string ipAddress,
            string device,
            string country,
            string region,
            string city)
        {
            return $"Your account has been temporarily locked due to {maxFailedAttempts} failed authentication attempts. " +
                   $"The last attempt was from {device} in {city}, {region}, {country} ({ipAddress}). " +
                   $"Please wait before trying again.";
        }

        public static string WebDavTokenResetTitle => "WebDAV access token changed";

        public static string WebDavTokenResetContent(
            string ipAddress,
            string device,
            string country,
            string region,
            string city)
        {
            return $"Your WebDAV access token has been reset from {device} " +
                   $"in {city}, {region}, {country} ({ipAddress}). " +
                   $"You will need to update your WebDAV client with the new token.";
        }
    }
}
