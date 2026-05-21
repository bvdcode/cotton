namespace Cotton.Server.Services
{
    internal static class NotificationTemplateKeys
    {
        public const string FailedLoginAttemptTitle = "notifications:server.failedLoginAttempt.title";
        public const string FailedLoginAttemptWithDeviceContent = "notifications:server.failedLoginAttempt.content.withDevice";
        public const string FailedLoginAttemptWithoutDeviceContent = "notifications:server.failedLoginAttempt.content.withoutDevice";

        public const string SuccessfulLoginTitle = "notifications:server.successfulLogin.title";
        public const string SuccessfulLoginWithDeviceContent = "notifications:server.successfulLogin.content.withDevice";
        public const string SuccessfulLoginWithoutDeviceContent = "notifications:server.successfulLogin.content.withoutDevice";

        public const string OtpDisabledTitle = "notifications:server.otpDisabled.title";
        public const string OtpDisabledWithDeviceContent = "notifications:server.otpDisabled.content.withDevice";
        public const string OtpDisabledWithoutDeviceContent = "notifications:server.otpDisabled.content.withoutDevice";

        public const string OtpEnabledTitle = "notifications:server.otpEnabled.title";
        public const string OtpEnabledWithDeviceContent = "notifications:server.otpEnabled.content.withDevice";
        public const string OtpEnabledWithoutDeviceContent = "notifications:server.otpEnabled.content.withoutDevice";

        public const string TotpFailedAttemptTitle = "notifications:server.totpFailedAttempt.title";
        public const string TotpFailedAttemptWithDeviceContent = "notifications:server.totpFailedAttempt.content.withDevice";
        public const string TotpFailedAttemptWithoutDeviceContent = "notifications:server.totpFailedAttempt.content.withoutDevice";

        public const string TotpLockoutTitle = "notifications:server.totpLockout.title";
        public const string TotpLockoutWithDeviceContent = "notifications:server.totpLockout.content.withDevice";
        public const string TotpLockoutWithoutDeviceContent = "notifications:server.totpLockout.content.withoutDevice";

        public const string WebDavTokenResetTitle = "notifications:server.webDavTokenReset.title";
        public const string WebDavTokenResetWithDeviceContent = "notifications:server.webDavTokenReset.content.withDevice";
        public const string WebDavTokenResetWithoutDeviceContent = "notifications:server.webDavTokenReset.content.withoutDevice";

        public const string SharedFileDownloadedTitle = "notifications:server.sharedFileDownloaded.title";
        public const string SharedFileDownloadedWithDeviceContent = "notifications:server.sharedFileDownloaded.content.withDevice";
        public const string SharedFileDownloadedWithoutDeviceContent = "notifications:server.sharedFileDownloaded.content.withoutDevice";

        public const string UploadHashMismatchTitle = "notifications:server.uploadHashMismatch.title";
        public const string UploadHashMismatchContent = "notifications:server.uploadHashMismatch.content";

        public const string StorageChunkMissingTitle = "notifications:server.storageChunkMissing.title";
        public const string StorageChunkMissingContent = "notifications:server.storageChunkMissing.content";

        public const string AppUpdateAvailableTitle = "notifications:server.appUpdateAvailable.title";
        public const string AppUpdateAvailableContent = "notifications:server.appUpdateAvailable.content";

        public const string StoragePressureTitle = "notifications:server.storagePressure.title";
        public const string StoragePressureContent = "notifications:server.storagePressure.content";

        public const string DatabaseRestoreCompletedTitle = "notifications:server.databaseRestoreCompleted.title";
        public const string DatabaseRestoreCompletedContent = "notifications:server.databaseRestoreCompleted.content";
    }
}
