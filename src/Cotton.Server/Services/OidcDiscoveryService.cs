// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using EasyExtensions.AspNetCore.Exceptions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cotton.Server.Services
{
    /// <summary>Fetches OpenID Connect discovery, tokens, and user-info documents.</summary>
    public class OidcDiscoveryService(HttpClient _httpClient)
    {
        /// <summary>Named HTTP client used for OIDC provider calls.</summary>
        public const string HttpClientName = "Cotton.Oidc";
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        /// <summary>Loads the provider discovery document.</summary>
        public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(
            OidcProvider provider,
            CancellationToken ct)
        {
            string metadataAddress = $"{provider.Issuer.TrimEnd('/')}/.well-known/openid-configuration";
            var retriever = new HttpDocumentRetriever(_httpClient)
            {
                RequireHttps = true
            };

            try
            {
                return await OpenIdConnectConfigurationRetriever.GetAsync(metadataAddress, retriever, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new BadRequestException<OidcProvider>("OIDC discovery document could not be loaded.");
            }
        }

        /// <summary>Exchanges an authorization code for provider tokens.</summary>
        public async Task<OidcTokenResponse> ExchangeCodeAsync(
            OpenIdConnectConfiguration configuration,
            OidcProvider provider,
            string code,
            string redirectUri,
            string codeVerifier,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(configuration.TokenEndpoint))
            {
                throw new BadRequestException<OidcProvider>("OIDC provider does not publish a token endpoint.");
            }

            var formValues = new List<KeyValuePair<string, string>>
            {
                new("grant_type", "authorization_code"),
                new("code", code),
                new("redirect_uri", redirectUri),
                new("client_id", provider.ClientId),
                new("code_verifier", codeVerifier)
            };
            if (!string.IsNullOrWhiteSpace(provider.ClientSecretEncrypted))
            {
                formValues.Add(new("client_secret", provider.ClientSecretEncrypted));
            }

            using var content = new FormUrlEncodedContent(formValues);
            using HttpResponseMessage response = await _httpClient.PostAsync(configuration.TokenEndpoint, content, ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new BadRequestException<OidcProvider>("OIDC token exchange failed.");
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(ct);
            OidcTokenResponse? tokenResponse = await JsonSerializer.DeserializeAsync<OidcTokenResponse>(
                stream,
                JsonOptions,
                ct);
            if (tokenResponse is null || string.IsNullOrWhiteSpace(tokenResponse.IdToken))
            {
                throw new BadRequestException<OidcProvider>("OIDC token response did not include an ID token.");
            }

            return tokenResponse;
        }

        /// <summary>Loads optional user-info claims when the provider exposes an endpoint.</summary>
        public async Task<OidcUserInfoClaims?> TryGetUserInfoAsync(
            OpenIdConnectConfiguration configuration,
            string? accessToken,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(configuration.UserInfoEndpoint)
                || string.IsNullOrWhiteSpace(accessToken))
            {
                return null;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, configuration.UserInfoEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using HttpResponseMessage response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(ct);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            JsonElement root = document.RootElement;
            return new(
                ReadString(root, "sub"),
                ReadString(root, "email"),
                ReadBoolean(root, "email_verified"),
                ReadString(root, "name"),
                ReadString(root, "given_name"),
                ReadString(root, "family_name"),
                ReadString(root, "picture"),
                ReadString(root, "preferred_username"));
        }

        private static string? ReadString(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement value)
                || value.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            string? text = value.GetString();
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        private static bool? ReadBoolean(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => bool.TryParse(value.GetString(), out bool parsed) ? parsed : null,
                _ => null
            };
        }
    }
}
