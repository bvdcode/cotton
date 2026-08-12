// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton;
using Cotton.Server.Abstractions;
using Cotton.Server.Controllers;
using Cotton.Server.Extensions;
using Cotton.Server.Helpers;
using Cotton.Server.Models;
using Cotton.Server.Providers;
using EasyExtensions;
using EasyExtensions.Abstractions;
using EasyExtensions.AspNetCore.Authorization.Abstractions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using EasyExtensions.EntityFrameworkCore.Database;
using EasyExtensions.Helpers;
using EasyExtensions.Models.Enums;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;

namespace Cotton.Server.Services
{
    public class AuthSessionIssuer(
        CottonDbContext _dbContext,
        ITokenProvider _tokens,
        SettingsProvider _settings,
        IHttpContextAccessor _httpContextAccessor,
        INotificationsProvider _notifications,
        IGeoLookupService _geoLookup,
        ILogger<AuthSessionIssuer> _logger)
    {
        private const string UnknownGeoLabel = "Unknown";
        private const string DemoGeoLabel = "Demo";

        public async Task<TokenPairResponseDto> SignInAsync(
            User user,
            bool trustDevice,
            AuthType authType,
            CancellationToken cancellationToken = default)
        {
            var (dbToken, refreshToken) = await CreateRefreshTokenAsync(user, trustDevice, authType);
            string accessToken = CreateAccessToken(user, dbToken.SessionId!);
            await _dbContext.RefreshTokens.AddAsync(dbToken, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            AddRefreshTokenToCookies(refreshToken, trustDevice);

            HttpRequest request = GetRequest();
            await _notifications.SendSuccessfulLoginAsync(
                _geoLookup,
                _settings,
                _logger,
                user.Id,
                GetRequestIpAddress(request),
                request.Headers.UserAgent);

            return new()
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };
        }

        public string CreateAccessToken(User user, string sessionId)
        {
            return _tokens.CreateToken(x =>
            {
                return x.Add(JwtRegisteredClaimNames.Sub, user.Id.ToString())
                    .Add(JwtRegisteredClaimNames.Name, user.Username)
                    .Add(JwtRegisteredClaimNames.Sid, sessionId)
                    .Add(ClaimTypes.Name, user.Username)
                    .Add(ClaimTypes.Role, user.Role.ToString());
            });
        }

        public async Task<(ExtendedRefreshToken DbToken, string RefreshToken)> CreateRefreshTokenAsync(
            User user,
            bool trustDevice,
            AuthType authType,
            string? sessionId = null)
        {
            HttpRequest request = GetRequest();
            IPAddress ipAddress = GetRequestIpAddress(request);
            GeoLookupResult? lookup = await _geoLookup.TryLookupAsync(ipAddress);
            (string City, string Region, string Country) geo = ResolveRefreshTokenGeoFields(lookup);
            sessionId ??= StringHelpers.CreateRandomString(AuthController.RefreshTokenLength);
            string refreshToken = StringHelpers.CreateRandomString(AuthController.RefreshTokenLength);
            ExtendedRefreshToken dbToken = new()
            {
                RevokedAt = null,
                UserId = user.Id,
                City = geo.City,
                SessionId = sessionId,
                Region = geo.Region,
                IsTrusted = trustDevice,
                Country = geo.Country,
                AuthType = authType,
                IpAddress = ipAddress,
                UserAgent = request.Headers.UserAgent.ToString(),
                Token = HashRefreshToken(refreshToken),
                Device = ResolveDeviceName(request),
            };
            return (dbToken, refreshToken);
        }

        public void AddRefreshTokenToCookies(string refreshToken, bool trustDevice)
        {
            const int yearHours = 24 * 365;
            int sessionTimeoutHours = _settings.GetServerSettings().SessionTimeoutHours;
            GetResponse().Cookies.Append(AuthController.CookieRefreshTokenKey, refreshToken, new CookieOptions
            {
                Secure = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddHours(trustDevice ? yearHours : sessionTimeoutHours)
            });
        }

        public static string HashRefreshToken(string refreshToken)
        {
            return Hasher.ToHexStringHash(Hasher.HashData(Encoding.UTF8.GetBytes(refreshToken)));
        }

        private HttpRequest GetRequest()
        {
            return _httpContextAccessor.HttpContext?.Request
                ?? throw new InvalidOperationException("HTTP request is required to issue an auth session.");
        }

        private HttpResponse GetResponse()
        {
            return _httpContextAccessor.HttpContext?.Response
                ?? throw new InvalidOperationException("HTTP response is required to issue an auth session.");
        }

        private static IPAddress GetRequestIpAddress(HttpRequest request)
        {
            return Constants.IsPublicInstance
                ? IPAddress.Loopback
                : request.GetTrustedClientIPAddress();
        }

        private static (string City, string Region, string Country) ResolveRefreshTokenGeoFields(
            GeoLookupResult? lookup)
        {
            if (lookup is null && Constants.IsPublicInstance)
            {
                return (DemoGeoLabel, string.Empty, string.Empty);
            }

            return (
                NormalizeGeoField(lookup?.City),
                NormalizeGeoField(lookup?.Region),
                NormalizeGeoField(lookup?.Country));
        }

        private static string NormalizeGeoField(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? UnknownGeoLabel : value;
        }

        private static string ResolveDeviceName(HttpRequest request)
        {
            string? clientDeviceName = NormalizeDeviceName(request.Headers[CottonClientHeaders.DeviceName].FirstOrDefault());
            return clientDeviceName ?? UserAgentHelpers.GetDevice(request.Headers.UserAgent.ToString());
        }

        private static string? NormalizeDeviceName(string? value)
        {
            string? normalized = value?.Trim();
            if (string.IsNullOrEmpty(normalized))
            {
                return null;
            }

            return normalized;
        }
    }
}
