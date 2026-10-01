// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Database.Models;
using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using Cotton.Server.Models.Results;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using EasyExtensions.Abstractions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Cotton.Server.Handlers.Auth
{
    public record LoginWithPasswordRequest(
        LoginRequestDto Credentials,
        IPAddress ClientIpAddress,
        string UserAgent) : IRequest<ActionResult<AuthSessionResponseDto>>;

    public class LoginWithPasswordRequestHandler(
        IMediator mediator,
        IPasswordHashService hasher,
        INotificationsProvider notifications,
        IGeoLookupService geoLookup,
        AuthSessionIssuer sessionIssuer)
        : IRequestHandler<LoginWithPasswordRequest, ActionResult<AuthSessionResponseDto>>
    {
        public async Task<ActionResult<AuthSessionResponseDto>> Handle(
            LoginWithPasswordRequest request, CancellationToken cancellationToken)
        {
            LoginRequestDto credentials = request.Credentials;
            User? user = await mediator.Send(new ResolveLoginUserRequest(credentials), cancellationToken);
            if (user is null)
            {
                return new ApiProblemResult(StatusCodes.Status401Unauthorized,
                    "Invalid username or password", "unauthorized");
            }

            if (string.IsNullOrEmpty(user.PasswordPhc) || !hasher.Verify(credentials.Password, user.PasswordPhc))
            {
                await notifications.SendFailedLoginAttemptAsync(geoLookup, user.Id,
                    credentials.Username, request.ClientIpAddress, request.UserAgent);
                return new ApiProblemResult(StatusCodes.Status401Unauthorized,
                    "Invalid username or password", "unauthorized");
            }

            string? totpError = await mediator.Send(new ValidateLoginTotpRequest(
                user, credentials.TwoFactorCode, request.ClientIpAddress, request.UserAgent), cancellationToken);
            if (totpError is not null)
            {
                return new ApiProblemResult(StatusCodes.Status403Forbidden, totpError, "forbidden");
            }

            return await sessionIssuer.SignInAsync(user, credentials.TrustDevice,
                AuthType.Credentials, cancellationToken);
        }
    }
}
