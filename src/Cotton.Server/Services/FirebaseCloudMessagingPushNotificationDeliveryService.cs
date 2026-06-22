// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Models.Notifications;
using Cotton.Server.Providers;
using Google.Apis.Auth.OAuth2;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Sends Android remote push notifications through Firebase Cloud Messaging HTTP v1.
    /// </summary>
    public class FirebaseCloudMessagingPushNotificationDeliveryService(
        HttpClient _httpClient,
        SettingsProvider _settingsProvider,
        ILogger<FirebaseCloudMessagingPushNotificationDeliveryService> _logger) : IPushNotificationDeliveryService
    {
        private const string FirebaseMessagingScope = "https://www.googleapis.com/auth/firebase.messaging";
        private const string SharesChannelId = "cotton.shares";
        private const string SecurityChannelId = "cotton.security";
        private const string UnregisteredErrorCode = "UNREGISTERED";
        private const string FirebaseFcmErrorType = "type.googleapis.com/google.firebase.fcm.v1.FcmError";
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        /// <inheritdoc />
        public async Task<PushNotificationDeliveryResult> SendAsync(
            PushNotificationDeliveryRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!request.Payload.IsEligible
                || request.Payload.Category is null
                || string.IsNullOrWhiteSpace(request.Payload.Title)
                || string.IsNullOrWhiteSpace(request.Payload.Body))
            {
                return PushNotificationDeliveryResult.Failed("Push payload is not eligible for delivery.");
            }

            if (string.IsNullOrWhiteSpace(request.ProviderToken))
            {
                return PushNotificationDeliveryResult.InvalidToken(
                    HttpStatusCode.BadRequest,
                    "EMPTY_TOKEN",
                    "Provider token is empty.");
            }

            CottonServerSettings settings = _settingsProvider.GetServerSettings();
            if (string.IsNullOrWhiteSpace(settings.FcmProjectId)
                || string.IsNullOrWhiteSpace(settings.FcmServiceAccountJsonEncrypted))
            {
                return PushNotificationDeliveryResult.NotConfigured(
                    "Firebase Cloud Messaging project ID or service account JSON is not configured.");
            }

            string accessToken;
            try
            {
                accessToken = await CreateAccessTokenAsync(
                    settings.FcmServiceAccountJsonEncrypted,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create Firebase Cloud Messaging access token.");
                return PushNotificationDeliveryResult.Failed("Failed to create Firebase Cloud Messaging access token.");
            }

            using HttpRequestMessage httpRequest = CreateHttpRequest(
                settings.FcmProjectId.Trim(),
                accessToken,
                request);

            try
            {
                using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken);
                string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return PushNotificationDeliveryResult.Sent(ReadProviderMessageName(responseBody));
                }

                string? errorCode = ReadProviderErrorCode(responseBody);
                string? reason = ReadProviderErrorMessage(responseBody);
                if (IsInvalidTokenResponse(response.StatusCode, errorCode))
                {
                    return PushNotificationDeliveryResult.InvalidToken(response.StatusCode, errorCode, reason);
                }

                if (IsTransientFailure(response.StatusCode))
                {
                    return PushNotificationDeliveryResult.TransientFailure(response.StatusCode, errorCode, reason);
                }

                return PushNotificationDeliveryResult.Rejected(response.StatusCode, errorCode, reason);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Firebase Cloud Messaging request failed before a provider response.");
                return PushNotificationDeliveryResult.Failed("Firebase Cloud Messaging request failed.");
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Firebase Cloud Messaging request timed out.");
                return PushNotificationDeliveryResult.Failed("Firebase Cloud Messaging request timed out.");
            }
        }

        private static async Task<string> CreateAccessTokenAsync(
            string serviceAccountJson,
            CancellationToken cancellationToken)
        {
            ServiceAccountCredential serviceAccountCredential =
                CredentialFactory.FromJson<ServiceAccountCredential>(serviceAccountJson);
            GoogleCredential credential = serviceAccountCredential
                .ToGoogleCredential()
                .CreateScoped(FirebaseMessagingScope);
            ITokenAccess tokenAccess = (ITokenAccess)credential.UnderlyingCredential;

            return await tokenAccess.GetAccessTokenForRequestAsync(null, cancellationToken);
        }

        private static HttpRequestMessage CreateHttpRequest(
            string projectId,
            string accessToken,
            PushNotificationDeliveryRequest request)
        {
            Uri endpoint = new(
                "https://fcm.googleapis.com/v1/projects/"
                + Uri.EscapeDataString(projectId)
                + "/messages:send");
            HttpRequestMessage httpRequest = new(HttpMethod.Post, endpoint);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Content = new StringContent(
                CreateRequestBody(request).ToJsonString(JsonOptions),
                Encoding.UTF8,
                "application/json");
            return httpRequest;
        }

        private static JsonObject CreateRequestBody(PushNotificationDeliveryRequest request)
        {
            string channelId = GetAndroidChannelId(request.Payload.Category!.Value);
            JsonObject message = new()
            {
                ["token"] = request.ProviderToken.Trim(),
                ["notification"] = new JsonObject
                {
                    ["title"] = request.Payload.Title,
                    ["body"] = request.Payload.Body,
                },
                ["data"] = CreateDataObject(request.Payload.Data),
                ["android"] = new JsonObject
                {
                    ["priority"] = GetAndroidPriority(request.Payload.Category.Value),
                    ["notification"] = new JsonObject
                    {
                        ["channel_id"] = channelId,
                    },
                },
            };

            return new JsonObject
            {
                ["message"] = message,
            };
        }

        private static JsonObject CreateDataObject(IReadOnlyDictionary<string, string> data)
        {
            JsonObject result = new();
            foreach (KeyValuePair<string, string> item in data)
            {
                result[item.Key] = item.Value;
            }

            return result;
        }

        private static string GetAndroidChannelId(PushNotificationEventCategory category)
        {
            return category switch
            {
                PushNotificationEventCategory.SharedFile => SharesChannelId,
                PushNotificationEventCategory.AccessRequest => SharesChannelId,
                PushNotificationEventCategory.CommentMention => SharesChannelId,
                PushNotificationEventCategory.SecuritySession => SecurityChannelId,
                _ => SharesChannelId,
            };
        }

        private static string GetAndroidPriority(PushNotificationEventCategory category)
        {
            return category == PushNotificationEventCategory.SecuritySession
                ? "HIGH"
                : "NORMAL";
        }

        private static string? ReadProviderMessageName(string responseBody)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(responseBody);
                JsonElement root = document.RootElement;
                return root.TryGetProperty("name", out JsonElement name) && name.ValueKind == JsonValueKind.String
                    ? name.GetString()
                    : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? ReadProviderErrorCode(string responseBody)
        {
            return ReadFcmErrorCode(responseBody) ?? ReadProviderErrorProperty(responseBody, "status");
        }

        private static string? ReadProviderErrorMessage(string responseBody)
        {
            return ReadProviderErrorProperty(responseBody, "message");
        }

        private static string? ReadFcmErrorCode(string responseBody)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(responseBody);
                JsonElement root = document.RootElement;
                if (!root.TryGetProperty("error", out JsonElement error)
                    || error.ValueKind != JsonValueKind.Object
                    || !error.TryGetProperty("details", out JsonElement details)
                    || details.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                foreach (JsonElement detail in details.EnumerateArray())
                {
                    if (detail.ValueKind != JsonValueKind.Object
                        || !detail.TryGetProperty("@type", out JsonElement type)
                        || type.ValueKind != JsonValueKind.String
                        || !string.Equals(type.GetString(), FirebaseFcmErrorType, StringComparison.Ordinal)
                        || !detail.TryGetProperty("errorCode", out JsonElement errorCode)
                        || errorCode.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    return errorCode.GetString();
                }

                return null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? ReadProviderErrorProperty(string responseBody, string propertyName)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(responseBody);
                JsonElement root = document.RootElement;
                if (!root.TryGetProperty("error", out JsonElement error)
                    || error.ValueKind != JsonValueKind.Object
                    || !error.TryGetProperty(propertyName, out JsonElement value)
                    || value.ValueKind != JsonValueKind.String)
                {
                    return null;
                }

                return value.GetString();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static bool IsInvalidTokenResponse(HttpStatusCode statusCode, string? errorCode)
        {
            return statusCode == HttpStatusCode.NotFound
                && string.Equals(errorCode, UnregisteredErrorCode, StringComparison.Ordinal);
        }

        private static bool IsTransientFailure(HttpStatusCode statusCode)
        {
            return statusCode is HttpStatusCode.InternalServerError
                or HttpStatusCode.BadGateway
                or HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.GatewayTimeout
                || (int)statusCode == 429;
        }
    }
}
