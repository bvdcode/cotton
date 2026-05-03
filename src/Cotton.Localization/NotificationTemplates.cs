namespace Cotton.Localization
{
    public static class NotificationTemplates
    {
        public static string FailedLoginAttemptTitle => "Failed login attempt";

        public static string FailedLoginAttemptContentNoDevice(
            string username,
            string ipAddress,
            string country,
            string region,
            string city)
        {
            return $"Someone tried to log in to your account '{username}' but failed. " +
                   $"The attempt was made from {city}, {region}, {country} ({ipAddress}).";
        }

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

        public static string SuccessfulLoginContentNoDevice(
            string ipAddress,
            string country,
            string region,
            string city)
        {
            return $"Your account was accessed from {city}, {region}, {country} ({ipAddress}). " +
                   $"If this wasn't you, please secure your account immediately.";
        }

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

        public static string OtpDisabledTitle => "Two-factor authentication disabled";

        public static string OtpDisabledContentNoDevice(
            string ipAddress,
            string country,
            string region,
            string city)
        {
            return $"Two-factor authentication has been disabled for your account " +
                   $"from {city}, {region}, {country} ({ipAddress}). " +
                   $"Your account is less secure now. If this wasn't you, please secure your account immediately.";
        }

        public static string OtpDisabledContent(
            string ipAddress,
            string device,
            string country,
            string region,
            string city)
        {
            return $"Two-factor authentication has been disabled for your account from {device} " +
                   $"in {city}, {region}, {country} ({ipAddress}). " +
                   $"Your account is less secure now. If this wasn't you, please secure your account immediately.";
        }

        public static string OtpEnabledTitle => "Two-factor authentication activated";

        public static string OtpEnabledContentNoDevice(
            string ipAddress,
            string country,
            string region,
            string city)
        {
            return $"Two-factor authentication has been enabled for your account " +
                   $"from {city}, {region}, {country} ({ipAddress}). " +
                   $"Your account is now more secure.";
        }

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

        public static string TotpFailedAttemptContentNoDevice(
            int failedAttempts,
            string ipAddress,
            string country,
            string region,
            string city)
        {
            return $"An invalid two-factor authentication code was entered ({failedAttempts} failed attempt(s)). " +
                   $"The attempt was made from {city}, {region}, {country} ({ipAddress}). " +
                   $"If this wasn't you, your account may be under attack.";
        }

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

        public static string TotpLockoutContentNoDevice(
            int maxFailedAttempts,
            string ipAddress,
            string country,
            string region,
            string city)
        {
            return $"Your account has been temporarily locked due to {maxFailedAttempts} failed authentication attempts. " +
                   $"The last attempt was from {city}, {region}, {country} ({ipAddress}). " +
                   $"Please wait before trying again.";
        }

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

        public static string WebDavTokenResetContentNoDevice(
            string ipAddress,
            string country,
            string region,
            string city)
        {
            return $"Your WebDAV access token has been reset " +
                   $"from {city}, {region}, {country} ({ipAddress}). " +
                   $"You will need to update your WebDAV client with the new token.";
        }

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

        public static string SharedFileDownloadedTitle => "Shared file downloaded";

        public static string SharedFileDownloadedContentNoDevice(
            string fileName,
            string ipAddress,
            string country,
            string region,
            string city)
        {
            return $"Your shared file '{fileName}' was downloaded " +
                   $"from {city}, {region}, {country} ({ipAddress}).";
        }

        public static string SharedFileDownloadedContent(
            string fileName,
            string ipAddress,
            string device,
            string country,
            string region,
            string city)
        {
            return $"Your shared file '{fileName}' was downloaded from {device} " +
                   $"in {city}, {region}, {country} ({ipAddress}).";
        }

        public static string UploadHashMismatchTitle => "Upload verification failed";

        public static string UploadHashMismatchContent(
            string fileName,
            string proposedHash,
            string computedHash)
        {
            string proposedTail = "..." + proposedHash[^4..];
            string computedTail = "..." + computedHash[^4..];
            return $"We couldn't verify the integrity of your upload for '{fileName}'. " +
                   $"Please re-upload the file.\n\n" +
                   $"Proposed: {proposedTail}\n" +
                   $"Computed: {computedTail}";
        }

        public static string StorageChunkMissingTitle => "File data missing from storage";

        public static string StorageChunkMissingContent(string fileName)
        {
            return $"A storage consistency check detected that data for your file '{fileName}' " +
                   $"is missing from the underlying storage. " +
                   $"Please verify your storage integrity and re-upload this file.";
        }

        public static string DatabaseRestoreCompletedTitle => "Database restored automatically";

        public static string DatabaseRestoreCompletedContent(
            string backupId,
            string sourceDatabase,
            string sourceHost,
            string sourcePort,
            string serverTimezone,
            DateTime createdAtUtc,
            DateTime createdAtLocal,
            DateTime restoredAtUtc,
            DateTime restoredAtLocal)
        {
            return
                $"Automatic database restore completed successfully.\n\n" +
                $"Backup ID: {backupId}\n" +
                $"Source database: {sourceDatabase} ({sourceHost}:{sourcePort})\n\n" +
                $"Backup created (UTC): {createdAtUtc:yyyy-MM-dd HH:mm:ss} UTC\n" +
                $"Backup created ({serverTimezone}): {createdAtLocal:yyyy-MM-dd HH:mm:ss}\n" +
                $"Restore completed (UTC): {restoredAtUtc:yyyy-MM-dd HH:mm:ss} UTC\n" +
                $"Restore completed ({serverTimezone}): {restoredAtLocal:yyyy-MM-dd HH:mm:ss}";
        }
    }
}
