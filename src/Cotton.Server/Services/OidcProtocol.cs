// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using EasyExtensions.AspNetCore.Exceptions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Cotton.Server.Services
{
    internal static class OidcProtocol
    {
        private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(2);

        public static ClaimsPrincipal ValidateIdToken(
            OpenIdConnectConfiguration configuration,
            OidcProvider provider,
            string idToken,
            string nonce)
        {
            JwtSecurityTokenHandler handler = new()
            {
                MapInboundClaims = false,
            };
            TokenValidationParameters validationParameters = new()
            {
                ValidateIssuer = true,
                ValidIssuer = configuration.Issuer ?? provider.Issuer,
                ValidateAudience = true,
                ValidAudience = provider.ClientId,
                ValidateLifetime = true,
                ClockSkew = ClockSkew,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = configuration.SigningKeys,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                NameClaimType = "name",
            };

            ClaimsPrincipal principal = handler.ValidateToken(idToken, validationParameters, out SecurityToken token);
            if (token is not JwtSecurityToken jwt
                || string.Equals(jwt.Header.Alg, SecurityAlgorithms.None, StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException<OidcProvider>("OIDC ID token signature is invalid.");
            }

            string? tokenNonce = principal.FindFirstValue("nonce");
            if (!string.Equals(tokenNonce, nonce, StringComparison.Ordinal))
            {
                throw new BadRequestException<OidcProvider>("OIDC ID token nonce is invalid.");
            }

            return principal;
        }

        public static OidcIdentityClaims CreateClaims(
            string expectedIssuer,
            ClaimsPrincipal principal,
            OidcUserInfoClaims? userInfo)
        {
            string subject = ReadRequiredClaim(
                principal,
                "OIDC subject is missing.",
                JwtRegisteredClaimNames.Sub,
                ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(userInfo?.Subject)
                && !string.Equals(userInfo.Subject, subject, StringComparison.Ordinal))
            {
                throw new BadRequestException<OidcProvider>("OIDC user-info subject does not match the ID token.");
            }

            string issuer = principal.FindFirstValue(JwtRegisteredClaimNames.Iss) ?? expectedIssuer;
            return new OidcIdentityClaims(
                issuer,
                subject,
                FirstNonEmpty(
                    userInfo?.Email,
                    principal.FindFirstValue(JwtRegisteredClaimNames.Email),
                    principal.FindFirstValue("email"),
                    principal.FindFirstValue(ClaimTypes.Email)),
                userInfo?.EmailVerified ?? ReadBooleanClaim(principal, "email_verified"),
                FirstNonEmpty(userInfo?.Name, principal.FindFirstValue("name"), principal.FindFirstValue(ClaimTypes.Name)),
                FirstNonEmpty(userInfo?.GivenName, principal.FindFirstValue("given_name"), principal.FindFirstValue(ClaimTypes.GivenName)),
                FirstNonEmpty(userInfo?.FamilyName, principal.FindFirstValue("family_name"), principal.FindFirstValue(ClaimTypes.Surname)),
                FirstNonEmpty(userInfo?.Picture, principal.FindFirstValue("picture")),
                FirstNonEmpty(userInfo?.PreferredUsername, principal.FindFirstValue("preferred_username")));
        }

        public static string CreateOpaqueValue()
        {
            return WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        }

        public static string HashOpaqueValue(string value)
        {
            return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        }

        public static string CreateCodeChallenge(string codeVerifier)
        {
            return WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
        }

        public static string NormalizeReturnUrl(string? returnUrl)
        {
            string trimmed = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl.Trim();
            return trimmed.StartsWith('/') && !trimmed.StartsWith("//", StringComparison.Ordinal)
                ? trimmed
                : "/";
        }

        private static string ReadRequiredClaim(
            ClaimsPrincipal principal,
            string error,
            params string[] types)
        {
            string? value = types
                .Select(principal.FindFirstValue)
                .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new BadRequestException<OidcProvider>(error);
            }

            return value.Trim();
        }

        private static bool ReadBooleanClaim(ClaimsPrincipal principal, string type)
        {
            return bool.TryParse(principal.FindFirstValue(type), out bool parsed) && parsed;
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            return values
                .Select(value => value?.Trim())
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }
    }
}
