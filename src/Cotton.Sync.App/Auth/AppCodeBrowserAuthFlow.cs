// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Sdk.Auth;
using Cotton.Sync.App.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cotton.Sync.App.Auth
{
    /// <summary>
    /// Implements browser app-code authentication through the Cotton SDK.
    /// </summary>
    public class AppCodeBrowserAuthFlow : IAppCodeBrowserAuthFlow
    {
        private static readonly TimeSpan DefaultPollDelay = TimeSpan.FromSeconds(2);

        private readonly ICottonAuthClient _authClient;
        private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
        private readonly ILogger<AppCodeBrowserAuthFlow> _logger;
        private readonly IPlatformCommandService _platformCommands;
        private readonly Func<DateTime> _utcNow;

        /// <summary>
        /// Initializes a new instance of the <see cref="AppCodeBrowserAuthFlow" /> class.
        /// </summary>
        public AppCodeBrowserAuthFlow(
            ICottonAuthClient authClient,
            IPlatformCommandService platformCommands,
            Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
            ILogger<AppCodeBrowserAuthFlow>? logger = null,
            Func<DateTime>? utcNow = null)
        {
            _authClient = authClient ?? throw new ArgumentNullException(nameof(authClient));
            _platformCommands = platformCommands ?? throw new ArgumentNullException(nameof(platformCommands));
            _delayAsync = delayAsync ?? Task.Delay;
            _logger = logger ?? NullLogger<AppCodeBrowserAuthFlow>.Instance;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        /// <inheritdoc />
        public async Task<AuthSession> SignInAsync(
            AppCodeBrowserSignInRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(request.ApplicationName))
            {
                throw new ArgumentException("Application name is required.", nameof(request));
            }

            AppCodeAuthorizationSession session = await _authClient
                .StartAppCodeAsync(ToStartRequest(request), cancellationToken)
                .ConfigureAwait(false);
            await _platformCommands.OpenWebAsync(session.ApprovalUri, cancellationToken).ConfigureAwait(false);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ThrowIfSessionExpired(session);
                AppCodePollResult result;
                try
                {
                    result = await _authClient
                        .PollAppCodeAsync(session.PollToken, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception exception) when (IsRetriablePollException(exception, cancellationToken))
                {
                    _logger.LogWarning(exception, "Browser sign-in polling failed transiently. Retrying.");
                    await _delayAsync(GetRetryDelay(session.PollInterval), cancellationToken).ConfigureAwait(false);
                    continue;
                }
                if (result.Status == AppCodePollStatus.Approved)
                {
                    UserDto user = await _authClient.MeAsync(cancellationToken).ConfigureAwait(false);
                    return ToSession(user);
                }

                if (result.Status != AppCodePollStatus.Pending
                    && result.Status != AppCodePollStatus.TooManyRequests)
                {
                    throw CreateFailure(result);
                }

                await _delayAsync(GetRetryDelay(result.RetryAfter ?? session.PollInterval), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        private void ThrowIfSessionExpired(AppCodeAuthorizationSession session)
        {
            if (session.ExpiresAt.ToUniversalTime() > _utcNow().ToUniversalTime())
            {
                return;
            }

            throw new AppCodeBrowserSignInException(
                AppCodePollStatus.Expired,
                "Browser sign-in request expired.",
                "expired");
        }

        private static TimeSpan GetRetryDelay(TimeSpan delay)
        {
            return delay <= TimeSpan.Zero ? DefaultPollDelay : delay;
        }

        private static bool IsRetriablePollException(Exception exception, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            return exception is HttpRequestException or IOException or TimeoutException or TaskCanceledException;
        }

        private static AppCodeStartRequestDto ToStartRequest(AppCodeBrowserSignInRequest request)
        {
            return new AppCodeStartRequestDto
            {
                ApplicationName = request.ApplicationName.Trim(),
                ApplicationVersion = NormalizeOptional(request.ApplicationVersion),
                DeviceName = NormalizeOptional(request.DeviceName),
            };
        }

        private static AuthSession ToSession(UserDto user)
        {
            return new AuthSession(user.Id, user.Username, user.Email, user.IsTotpEnabled);
        }

        private static AppCodeBrowserSignInException CreateFailure(AppCodePollResult result)
        {
            string message = result.Status switch
            {
                AppCodePollStatus.Denied => "Browser sign-in was denied.",
                AppCodePollStatus.Expired => "Browser sign-in request expired.",
                AppCodePollStatus.NotFound => "Browser sign-in request was not found.",
                _ => "Browser sign-in failed.",
            };
            return new AppCodeBrowserSignInException(result.Status, message, result.Error);
        }

        private static string? NormalizeOptional(string? value)
        {
            string? trimmed = value?.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }
    }
}
