// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Localization
{
    public static class NotificationTemplates
    {
        public static string FailedLoginAttemptTitle => "Failed login attempt";

        public static string FailedLoginAttemptContentNoDevice(
            string username,
            string ipAddress,
            string location)
        {
            return $"Someone tried to log in to your account '{username}' but failed. " +
                   $"The attempt was made from {location} ({ipAddress}).";
        }

        public static string FailedLoginAttemptContent(
            string username,
            string ipAddress,
            string device,
            string location)
        {
            return $"Someone tried to log in to your account '{username}' but failed. " +
                   $"The attempt was made from {device} in {location} ({ipAddress}).";
        }

        public static string SuccessfulLoginTitle => "New login to your account";

        public static string SuccessfulLoginContentNoDevice(
            string ipAddress,
            string location)
        {
            return $"Your account was accessed from {location} ({ipAddress}). " +
                   $"If this wasn't you, please secure your account immediately.";
        }

        public static string SuccessfulLoginContent(
            string ipAddress,
            string device,
            string location)
        {
            return $"Your account was accessed from {device} in {location} ({ipAddress}). " +
                   $"If this wasn't you, please secure your account immediately.";
        }

        public static string OtpDisabledTitle => "Two-factor authentication disabled";

        public static string OtpDisabledContentNoDevice(
            string ipAddress,
            string location)
        {
            return $"Two-factor authentication has been disabled for your account " +
                   $"from {location} ({ipAddress}). " +
                   $"Your account is less secure now. If this wasn't you, please secure your account immediately.";
        }

        public static string OtpDisabledContent(
            string ipAddress,
            string device,
            string location)
        {
            return $"Two-factor authentication has been disabled for your account from {device} " +
                   $"in {location} ({ipAddress}). " +
                   $"Your account is less secure now. If this wasn't you, please secure your account immediately.";
        }

        public static string OtpEnabledTitle => "Two-factor authentication activated";

        public static string OtpEnabledContentNoDevice(
            string ipAddress,
            string location)
        {
            return $"Two-factor authentication has been enabled for your account " +
                   $"from {location} ({ipAddress}). " +
                   $"Your account is now more secure.";
        }

        public static string OtpEnabledContent(
            string ipAddress,
            string device,
            string location)
        {
            return $"Two-factor authentication has been enabled for your account from {device} " +
                   $"in {location} ({ipAddress}). " +
                   $"Your account is now more secure.";
        }

        public static string TotpFailedAttemptTitle => "Invalid authentication code";

        public static string TotpFailedAttemptContentNoDevice(
            int failedAttempts,
            string ipAddress,
            string location)
        {
            return $"An invalid two-factor authentication code was entered ({failedAttempts} failed attempt(s)). " +
                   $"The attempt was made from {location} ({ipAddress}). " +
                   $"If this wasn't you, your account may be under attack.";
        }

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

        public static string TotpLockoutTitle => "Account temporarily locked";

        public static string TotpLockoutContentNoDevice(
            int maxFailedAttempts,
            string ipAddress,
            string location)
        {
            return $"Your account has been temporarily locked due to {maxFailedAttempts} failed authentication attempts. " +
                   $"The last attempt was from {location} ({ipAddress}). " +
                   $"Please wait before trying again.";
        }

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

        public static string WebDavTokenResetTitle => "WebDAV access token changed";

        public static string WebDavTokenResetContentNoDevice(
            string ipAddress,
            string location)
        {
            return $"Your WebDAV access token has been reset " +
                   $"from {location} ({ipAddress}). " +
                   $"You will need to update your WebDAV client with the new token.";
        }

        public static string WebDavTokenResetContent(
            string ipAddress,
            string device,
            string location)
        {
            return $"Your WebDAV access token has been reset from {device} " +
                   $"in {location} ({ipAddress}). " +
                   $"You will need to update your WebDAV client with the new token.";
        }

        public static string PasswordChangedTitle => "Password changed";

        public static string PasswordChangedContent =>
            "Your account password was changed and existing sessions were revoked.";

        public static string PasswordResetCompletedTitle => "Password reset completed";

        public static string PasswordResetCompletedContent =>
            "Your account password was reset and existing sessions were revoked.";

        public static string EmailChangedTitle => "Account email changed";

        public static string EmailChangedContent(string? previousEmail, string? newEmail)
        {
            return $"Your account email was changed from {FormatOptionalEmail(previousEmail)} " +
                   $"to {FormatOptionalEmail(newEmail)}.";
        }

        public static string PasskeyAddedTitle => "Passkey added";

        public static string PasskeyAddedContent(string passkeyName)
        {
            return $"A passkey was added to your account: {passkeyName}.";
        }

        public static string PasskeyRemovedTitle => "Passkey removed";

        public static string PasskeyRemovedContent(string passkeyName)
        {
            return $"A passkey was removed from your account: {passkeyName}.";
        }

        public static string ExternalIdentityLinkedTitle => "External account linked";

        public static string ExternalIdentityLinkedContent(string providerName)
        {
            return $"An external sign-in account was linked through {providerName}.";
        }

        public static string ExternalIdentityUnlinkedTitle => "External account unlinked";

        public static string ExternalIdentityUnlinkedContent(string providerName)
        {
            return $"An external sign-in account was unlinked from {providerName}.";
        }

        private static string FormatOptionalEmail(string? email)
        {
            return string.IsNullOrWhiteSpace(email) ? "no email address" : email.Trim();
        }

        public static string SharedFileDownloadedTitle => "Shared file downloaded";

        public static string SharedFileDownloadedContentNoDevice(
            string fileName,
            string ipAddress,
            string location)
        {
            return $"Your shared file '{fileName}' was downloaded " +
                   $"from {location} ({ipAddress}).";
        }

        public static string SharedFileDownloadedContent(
            string fileName,
            string ipAddress,
            string device,
            string location)
        {
            return $"Your shared file '{fileName}' was downloaded from {device} " +
                   $"in {location} ({ipAddress}).";
        }

        public static string UploadHashMismatchTitle => "Upload verification failed";

        public static string FormatHashTail(string hash)
        {
            return "..." + hash[^4..];
        }

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

        public static string StorageChunkMissingTitle => "File data missing from storage";

        public static string StorageChunkMissingContent(string fileName)
        {
            return $"A storage consistency check detected that data for your file '{fileName}' " +
                   $"is missing from the underlying storage. " +
                   $"Please verify your storage integrity and re-upload this file.";
        }

        public static string AppUpdateAvailableTitle => "Cotton server update available";

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

        public static string AppCodeApprovalTitle => "Application sign-in approved";

        public static string AppCodeApprovalContent(
            string applicationName,
            string applicationVersion,
            string origin)
        {
            return $"{applicationName} {applicationVersion} signed in from {origin}.";
        }

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

        public static string StoragePressureTitle => "Storage is running out of free space";

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

        public static string DatabaseRestoreCompletedTitle => "Database restored automatically";

        public static string DatabaseIntegrityFailureTitle => "Database integrity issue detected";

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
