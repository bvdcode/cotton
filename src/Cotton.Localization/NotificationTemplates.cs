// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Localization
{
    /// <summary>
    /// English server-side notification templates used when no client-side localization template is available.
    /// </summary>
    public static class NotificationTemplates
    {
        /// <summary>
        /// Title for a failed password login notification.
        /// </summary>
        public static string FailedLoginAttemptTitle => "Failed login attempt";

        /// <summary>
        /// Builds failed login notification content when no device name is known.
        /// </summary>
        public static string FailedLoginAttemptContentNoDevice(
            string username,
            string ipAddress,
            string location)
        {
            return $"Someone tried to log in to your account '{username}' but failed. " +
                   $"The attempt was made from {location} ({ipAddress}).";
        }

        /// <summary>
        /// Builds failed login notification content with device details.
        /// </summary>
        public static string FailedLoginAttemptContent(
            string username,
            string ipAddress,
            string device,
            string location)
        {
            return $"Someone tried to log in to your account '{username}' but failed. " +
                   $"The attempt was made from {device} in {location} ({ipAddress}).";
        }

        /// <summary>
        /// Title for a successful login notification.
        /// </summary>
        public static string SuccessfulLoginTitle => "New login to your account";

        /// <summary>
        /// Builds successful login notification content when no device name is known.
        /// </summary>
        public static string SuccessfulLoginContentNoDevice(
            string ipAddress,
            string location)
        {
            return $"Your account was accessed from {location} ({ipAddress}). " +
                   $"If this wasn't you, please secure your account immediately.";
        }

        /// <summary>
        /// Builds successful login notification content with device details.
        /// </summary>
        public static string SuccessfulLoginContent(
            string ipAddress,
            string device,
            string location)
        {
            return $"Your account was accessed from {device} in {location} ({ipAddress}). " +
                   $"If this wasn't you, please secure your account immediately.";
        }

        /// <summary>
        /// Title for a two-factor disabled notification.
        /// </summary>
        public static string OtpDisabledTitle => "Two-factor authentication disabled";

        /// <summary>
        /// Builds two-factor disabled notification content when no device name is known.
        /// </summary>
        public static string OtpDisabledContentNoDevice(
            string ipAddress,
            string location)
        {
            return $"Two-factor authentication has been disabled for your account " +
                   $"from {location} ({ipAddress}). " +
                   $"Your account is less secure now. If this wasn't you, please secure your account immediately.";
        }

        /// <summary>
        /// Builds two-factor disabled notification content with device details.
        /// </summary>
        public static string OtpDisabledContent(
            string ipAddress,
            string device,
            string location)
        {
            return $"Two-factor authentication has been disabled for your account from {device} " +
                   $"in {location} ({ipAddress}). " +
                   $"Your account is less secure now. If this wasn't you, please secure your account immediately.";
        }

        /// <summary>
        /// Title for a two-factor enabled notification.
        /// </summary>
        public static string OtpEnabledTitle => "Two-factor authentication activated";

        /// <summary>
        /// Builds two-factor enabled notification content when no device name is known.
        /// </summary>
        public static string OtpEnabledContentNoDevice(
            string ipAddress,
            string location)
        {
            return $"Two-factor authentication has been enabled for your account " +
                   $"from {location} ({ipAddress}). " +
                   $"Your account is now more secure.";
        }

        /// <summary>
        /// Builds two-factor enabled notification content with device details.
        /// </summary>
        public static string OtpEnabledContent(
            string ipAddress,
            string device,
            string location)
        {
            return $"Two-factor authentication has been enabled for your account from {device} " +
                   $"in {location} ({ipAddress}). " +
                   $"Your account is now more secure.";
        }

        /// <summary>
        /// Title for an invalid TOTP attempt notification.
        /// </summary>
        public static string TotpFailedAttemptTitle => "Invalid authentication code";

        /// <summary>
        /// Builds invalid TOTP attempt content when no device name is known.
        /// </summary>
        public static string TotpFailedAttemptContentNoDevice(
            int failedAttempts,
            string ipAddress,
            string location)
        {
            return $"An invalid two-factor authentication code was entered ({failedAttempts} failed attempt(s)). " +
                   $"The attempt was made from {location} ({ipAddress}). " +
                   $"If this wasn't you, your account may be under attack.";
        }

        /// <summary>
        /// Builds invalid TOTP attempt content with device details.
        /// </summary>
        public static string TotpFailedAttemptContent(
            int failedAttempts,
            string ipAddress,
            string device,
            string location)
        {
            return $"An invalid two-factor authentication code was entered ({failedAttempts} failed attempt(s)). " +
                   $"The attempt was made from {device} in {location} ({ipAddress}). " +
                   $"If this wasn't you, your account may be under attack.";
        }

        /// <summary>
        /// Title for a temporary TOTP lockout notification.
        /// </summary>
        public static string TotpLockoutTitle => "Account temporarily locked";

        /// <summary>
        /// Builds TOTP lockout content when no device name is known.
        /// </summary>
        public static string TotpLockoutContentNoDevice(
            int maxFailedAttempts,
            string ipAddress,
            string location)
        {
            return $"Your account has been temporarily locked due to {maxFailedAttempts} failed authentication attempts. " +
                   $"The last attempt was from {location} ({ipAddress}). " +
                   $"Please wait before trying again.";
        }

        /// <summary>
        /// Builds TOTP lockout content with device details.
        /// </summary>
        public static string TotpLockoutContent(
            int maxFailedAttempts,
            string ipAddress,
            string device,
            string location)
        {
            return $"Your account has been temporarily locked due to {maxFailedAttempts} failed authentication attempts. " +
                   $"The last attempt was from {device} in {location} ({ipAddress}). " +
                   $"Please wait before trying again.";
        }

        /// <summary>
        /// Title for a WebDAV token reset notification.
        /// </summary>
        public static string WebDavTokenResetTitle => "WebDAV access token changed";

        /// <summary>
        /// Builds WebDAV token reset content when no device name is known.
        /// </summary>
        public static string WebDavTokenResetContentNoDevice(
            string ipAddress,
            string location)
        {
            return $"Your WebDAV access token has been reset " +
                   $"from {location} ({ipAddress}). " +
                   $"You will need to update your WebDAV client with the new token.";
        }

        /// <summary>
        /// Builds WebDAV token reset content with device details.
        /// </summary>
        public static string WebDavTokenResetContent(
            string ipAddress,
            string device,
            string location)
        {
            return $"Your WebDAV access token has been reset from {device} " +
                   $"in {location} ({ipAddress}). " +
                   $"You will need to update your WebDAV client with the new token.";
        }

        /// <summary>
        /// Title for a shared file download notification.
        /// </summary>
        public static string SharedFileDownloadedTitle => "Shared file downloaded";

        /// <summary>
        /// Builds shared file download content when no device name is known.
        /// </summary>
        public static string SharedFileDownloadedContentNoDevice(
            string fileName,
            string ipAddress,
            string location)
        {
            return $"Your shared file '{fileName}' was downloaded " +
                   $"from {location} ({ipAddress}).";
        }

        /// <summary>
        /// Builds shared file download content with device details.
        /// </summary>
        public static string SharedFileDownloadedContent(
            string fileName,
            string ipAddress,
            string device,
            string location)
        {
            return $"Your shared file '{fileName}' was downloaded from {device} " +
                   $"in {location} ({ipAddress}).";
        }

        /// <summary>
        /// Title for an upload hash mismatch notification.
        /// </summary>
        public static string UploadHashMismatchTitle => "Upload verification failed";

        /// <summary>
        /// Formats a hash for compact display in notifications.
        /// </summary>
        public static string FormatHashTail(string hash)
        {
            return "..." + hash[^4..];
        }

        /// <summary>
        /// Builds upload hash mismatch notification content.
        /// </summary>
        public static string UploadHashMismatchContent(
            string fileName,
            string proposedHash,
            string computedHash)
        {
            string proposedTail = FormatHashTail(proposedHash);
            string computedTail = FormatHashTail(computedHash);
            return $"We couldn't verify the integrity of your upload for '{fileName}'. " +
                   $"Please re-upload the file.\n\n" +
                   $"Proposed: {proposedTail}\n" +
                   $"Computed: {computedTail}";
        }

        /// <summary>
        /// Title for missing storage chunk notifications.
        /// </summary>
        public static string StorageChunkMissingTitle => "File data missing from storage";

        /// <summary>
        /// Builds missing storage chunk notification content.
        /// </summary>
        public static string StorageChunkMissingContent(string fileName)
        {
            return $"A storage consistency check detected that data for your file '{fileName}' " +
                   $"is missing from the underlying storage. " +
                   $"Please verify your storage integrity and re-upload this file.";
        }

        /// <summary>
        /// Title for an available Cotton server update notification.
        /// </summary>
        public static string AppUpdateAvailableTitle => "Cotton server update available";

        /// <summary>
        /// Builds available Cotton server update notification content.
        /// </summary>
        public static string AppUpdateAvailableContent(
            string currentVersion,
            string latestVersion,
            string releaseUrl,
            string? releaseNotes)
        {
            return
                $"Current server version: {currentVersion}\n" +
                $"Available server version: {latestVersion}\n\n" +
                $"Release notes:\n" +
                FormatReleaseNotes(releaseNotes) +
                $"\n\nFull release: {releaseUrl}";
        }

        /// <summary>
        /// Title for app-code approval notifications.
        /// </summary>
        public static string AppCodeApprovalTitle => "Application sign-in approved";

        /// <summary>
        /// Builds app-code approval notification content.
        /// </summary>
        public static string AppCodeApprovalContent(
            string applicationName,
            string applicationVersion,
            string origin)
        {
            return $"{applicationName} {applicationVersion} signed in from {origin}.";
        }

        /// <summary>
        /// Normalizes and truncates release notes for notification display.
        /// </summary>
        public static string FormatReleaseNotes(string? releaseNotes)
        {
            const int maxLength = 3000;
            string normalized = (releaseNotes ?? string.Empty).Replace("\r\n", "\n").Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return "No release notes were published for this release.";
            }

            return normalized.Length <= maxLength
                ? normalized
                : normalized[..maxLength].TrimEnd() + "...";
        }

        /// <summary>
        /// Title for local storage pressure notifications.
        /// </summary>
        public static string StoragePressureTitle => "Storage is running out of free space";

        /// <summary>
        /// Builds local storage pressure notification content.
        /// </summary>
        public static string StoragePressureContent(
            string availableSpace,
            double availablePercent,
            string requiredReserve,
            string rootPath)
        {
            return "Cotton paused new storage writes because the local storage reserve would be crossed. " +
                   $"Free space: {availableSpace} ({availablePercent:F1}%). " +
                   $"Required reserve: {requiredReserve}. " +
                   $"Storage root: {rootPath}. Free disk space or expand the volume, then retry the upload.";
        }

        /// <summary>
        /// Title for automatic database restore completion notifications.
        /// </summary>
        public static string DatabaseRestoreCompletedTitle => "Database restored automatically";

        /// <summary>
        /// Title for database integrity failure notifications.
        /// </summary>
        public static string DatabaseIntegrityFailureTitle => "Database integrity issue detected";

        /// <summary>
        /// Builds database integrity failure notification content.
        /// </summary>
        public static string DatabaseIntegrityFailureContent(
            string entityName,
            string entityKey,
            string boundary,
            DateTime detectedAtUtc)
        {
            return
                $"Cotton rejected a protected database row because its integrity signature did not match.\n\n" +
                $"Entity: {entityName}\n" +
                $"Row: {entityKey}\n" +
                $"Boundary: {boundary}\n" +
                $"Detected (UTC): {detectedAtUtc:yyyy-MM-dd HH:mm:ss}\n\n" +
                "If you edited PostgreSQL manually, restore the row from a trusted backup or re-apply the change through Cotton.";
        }

        /// <summary>
        /// Builds automatic database restore completion notification content.
        /// </summary>
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
