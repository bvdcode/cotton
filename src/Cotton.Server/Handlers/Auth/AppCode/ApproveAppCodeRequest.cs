// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Localization;
using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using Cotton.Server.Models;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using EasyExtensions;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.EntityFrameworkCore.Database;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using EasyExtensions.Models.Enums;
using System.Net;

namespace Cotton.Server.Handlers.Auth.AppCode
{
    public record ApproveAppCodeRequest(Guid Id, Guid UserId) : IRequest;

    public class ApproveAppCodeRequestHandler(
        CottonDbContext dbContext,
        AuthSessionIssuer sessionIssuer,
        INotificationsProvider notifications,
        IDatabaseIntegrityVerifier integrity,
        IGeoLookupService geoLookup,
        SettingsProvider settings,
        AppCodeRequestStore store,
        ILogger<ApproveAppCodeRequestHandler> logger) : IRequestHandler<ApproveAppCodeRequest>
    {
        public async Task Handle(ApproveAppCodeRequest request, CancellationToken cancellationToken)
        {
            AppCodeRequestState state = AppCodeRequestGuard.Get(store, request.Id);
            await state.Gate.WaitAsync(cancellationToken);
            try
            {
                AppCodeRequestGuard.EnsurePending(store, state);
                User user = await dbContext.Users.FindAsync([request.UserId], cancellationToken)
                    ?? throw new EntityNotFoundException<User>("Current user not found.");
                integrity.RequireValid(dbContext, user, "oauth.app-code.approve-user");

                (ExtendedRefreshToken dbToken, string refreshToken) = await sessionIssuer
                    .CreateRefreshTokenAsync(user, trustDevice: true, AuthType.Credentials);
                await ApplyApplicationSessionMetadataAsync(dbToken, state, cancellationToken);

                string accessToken = sessionIssuer.CreateAccessToken(user, dbToken.SessionId!);
                await dbContext.RefreshTokens.AddAsync(dbToken, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);

                state.Tokens = new TokenPairResponseDto
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                };
                state.Status = AppCodeRequestStatus.Approved;
                state.ApprovedAt = DateTime.UtcNow;

                await SendApprovedSecurityEventAsync(request.UserId, state);
                state.Completion.TrySetResult();
            }
            finally
            {
                state.Gate.Release();
            }
        }

        private async Task ApplyApplicationSessionMetadataAsync(
            ExtendedRefreshToken dbToken,
            AppCodeRequestState state,
            CancellationToken cancellationToken)
        {
            dbToken.Device = BuildSessionDeviceName(state);
            dbToken.UserAgent = state.UserAgent;
            if (!IPAddress.TryParse(state.Origin, out IPAddress? originAddress))
            {
                return;
            }

            dbToken.IpAddress = originAddress;
            GeoLookupResult? lookup = await geoLookup.TryLookupAsync(originAddress, cancellationToken);
            dbToken.Country = NormalizeGeoField(lookup?.Country);
            dbToken.Region = NormalizeGeoField(lookup?.Region);
            dbToken.City = NormalizeGeoField(lookup?.City);
        }

        private async Task SendApprovedSecurityEventAsync(Guid userId, AppCodeRequestState state)
        {
            string notificationContent = NotificationTemplates.AppCodeApprovalContent(
                state.ApplicationName, state.ApplicationVersion, origin: null);
            string emailContent = NotificationTemplates.AppCodeApprovalContent(
                state.ApplicationName, state.ApplicationVersion, state.Origin);
            try
            {
                Dictionary<string, string> metadata = new()
                {
                    ["applicationName"] = state.ApplicationName,
                    ["applicationVersion"] = state.ApplicationVersion,
                    ["origin"] = state.Origin,
                    ["requestId"] = state.ApprovalId.ToString("D"),
                };
                Dictionary<string, string> templateMetadata = NotificationTemplateMetadata.Create(
                    NotificationTemplateKeys.AppCodeApprovalTitle,
                    NotificationTemplateKeys.AppCodeApprovalContent,
                    metadata);
                await notifications.SendNotificationAsync(
                    userId,
                    NotificationTemplates.AppCodeApprovalTitle,
                    notificationContent,
                    NotificationPriority.Medium,
                    templateMetadata);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to send app-code approval notification for request {RequestId}", state.ApprovalId);
            }

            await notifications.SendSecurityEmailAsync(
                settings,
                logger,
                userId,
                NotificationTemplates.AppCodeApprovalTitle,
                emailContent,
                state.ApprovedAt ?? DateTime.UtcNow);
        }

        private static string BuildSessionDeviceName(AppCodeRequestState state)
        {
            string app = state.ApplicationVersion == "Unknown version"
                ? state.ApplicationName
                : $"{state.ApplicationName} {state.ApplicationVersion}";
            return string.IsNullOrWhiteSpace(state.DeviceName)
                ? app
                : $"{app} on {state.DeviceName}";
        }

        private static string NormalizeGeoField(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
        }
    }
}
