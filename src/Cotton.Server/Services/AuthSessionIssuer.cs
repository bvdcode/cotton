// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
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
using EasyExtensions.Extensions;
using EasyExtensions.Helpers;
using EasyExtensions.Models.Enums;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;

namespace Cotton.Server.Services;

/// <summary>Issues Cotton access and refresh sessions after an authentication factor succeeds.</summary>
public sealed class AuthSessionIssuer(
    CottonDbContext _dbContext,
    ITokenProvider _tokens,
    SettingsProvider _settings,
    IHttpContextAccessor _httpContextAccessor,
    INotificationsProvider _notifications,
    IGeoLookupService _geoLookup)
{
    private const string UnknownGeoLabel = "Unknown";
    private const string DemoGeoLabel = "Demo";

    /// <summary>Creates auth cookies and returns the API token response.</summary>
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
        AddAccessTokenToCookies(accessToken);

        HttpRequest request = GetRequest();
        await _notifications.SendSuccessfulLoginAsync(
            _geoLookup,
            user.Id,
            GetRequestIpAddress(request),
            request.Headers.UserAgent);

        return new()
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
        };
    }

    /// <summary>Creates a signed JWT access token for an existing session id.</summary>
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

    /// <summary>Creates a stored refresh-token row and returns its plaintext token value.</summary>
    public async Task<(ExtendedRefreshToken DbToken, string RefreshToken)> CreateRefreshTokenAsync(
        User user,
        bool trustDevice,
        AuthType authType,
        string? sessionId = null)
    {
        HttpRequest request = GetRequest();
        IPAddress ipAddress = GetRequestIpAddress(request);
        var lookup = await _geoLookup.TryLookupAsync(ipAddress);
        var geo = ResolveRefreshTokenGeoFields(lookup);
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
            Device = UserAgentHelpers.GetDevice(request.Headers.UserAgent.ToString()),
        };
        return (dbToken, refreshToken);
    }

    /// <summary>Adds the refresh token to the current response cookies.</summary>
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

    /// <summary>Adds the access token to the current response cookies.</summary>
    public void AddAccessTokenToCookies(string accessToken)
    {
        GetResponse().Cookies.Append(AuthController.CookieAccessTokenKey, accessToken, new CookieOptions
        {
            Secure = true,
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.Add(_tokens.TokenLifetime)
        });
    }

    /// <summary>Hashes a plaintext refresh token for storage and lookup.</summary>
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
            : request.GetRemoteIPAddress();
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
}
