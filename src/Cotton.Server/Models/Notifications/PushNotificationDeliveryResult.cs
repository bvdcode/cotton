// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net;

namespace Cotton.Server.Models.Notifications
{
    /// <summary>
    /// Represents the outcome of a remote push delivery attempt.
    /// </summary>
    public class PushNotificationDeliveryResult
    {
        private PushNotificationDeliveryResult(
            PushNotificationDeliveryStatus status,
            string? providerMessageName,
            HttpStatusCode? statusCode,
            string? errorCode,
            string? reason)
        {
            Status = status;
            ProviderMessageName = providerMessageName;
            StatusCode = statusCode;
            ErrorCode = errorCode;
            Reason = reason;
        }

        /// <summary>
        /// Gets the delivery status.
        /// </summary>
        public PushNotificationDeliveryStatus Status { get; }

        /// <summary>
        /// Gets the provider message name returned by FCM on success.
        /// </summary>
        public string? ProviderMessageName { get; }

        /// <summary>
        /// Gets the HTTP status code returned by the provider.
        /// </summary>
        public HttpStatusCode? StatusCode { get; }

        /// <summary>
        /// Gets the provider error code when available.
        /// </summary>
        public string? ErrorCode { get; }

        /// <summary>
        /// Gets a short delivery reason for logs.
        /// </summary>
        public string? Reason { get; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        public static PushNotificationDeliveryResult Sent(string? providerMessageName)
        {
            return new PushNotificationDeliveryResult(
                PushNotificationDeliveryStatus.Sent,
                providerMessageName,
                HttpStatusCode.OK,
                null,
                null);
        }

        /// <summary>
        /// Creates a skipped not-configured result.
        /// </summary>
        public static PushNotificationDeliveryResult NotConfigured(string reason)
        {
            return new PushNotificationDeliveryResult(
                PushNotificationDeliveryStatus.NotConfigured,
                null,
                null,
                null,
                reason);
        }

        /// <summary>
        /// Creates an invalid token result.
        /// </summary>
        public static PushNotificationDeliveryResult InvalidToken(
            HttpStatusCode statusCode,
            string? errorCode,
            string? reason)
        {
            return new PushNotificationDeliveryResult(
                PushNotificationDeliveryStatus.InvalidToken,
                null,
                statusCode,
                errorCode,
                reason);
        }

        /// <summary>
        /// Creates a rejected result.
        /// </summary>
        public static PushNotificationDeliveryResult Rejected(
            HttpStatusCode statusCode,
            string? errorCode,
            string? reason)
        {
            return new PushNotificationDeliveryResult(
                PushNotificationDeliveryStatus.Rejected,
                null,
                statusCode,
                errorCode,
                reason);
        }

        /// <summary>
        /// Creates a transient failure result.
        /// </summary>
        public static PushNotificationDeliveryResult TransientFailure(
            HttpStatusCode statusCode,
            string? errorCode,
            string? reason)
        {
            return new PushNotificationDeliveryResult(
                PushNotificationDeliveryStatus.TransientFailure,
                null,
                statusCode,
                errorCode,
                reason);
        }

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        public static PushNotificationDeliveryResult Failed(string reason)
        {
            return new PushNotificationDeliveryResult(
                PushNotificationDeliveryStatus.Failed,
                null,
                null,
                null,
                reason);
        }
    }
}
