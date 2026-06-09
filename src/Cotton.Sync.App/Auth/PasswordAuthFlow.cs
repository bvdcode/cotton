// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Sdk.Auth;

namespace Cotton.Sync.App.Auth
{

    /// <summary>
    /// Implements username/password desktop authentication through the Cotton SDK.
    /// </summary>
    public sealed class PasswordAuthFlow : IAuthFlow
    {
        private readonly ICottonAuthClient _authClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="PasswordAuthFlow" /> class.
        /// </summary>
        public PasswordAuthFlow(ICottonAuthClient authClient)
        {
            _authClient = authClient ?? throw new ArgumentNullException(nameof(authClient));
        }

        /// <inheritdoc />
        public async Task<AuthSession> SignInAsync(
            PasswordSignInRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(request.Username))
            {
                throw new ArgumentException("Username is required.", nameof(request));
            }

            if (string.IsNullOrEmpty(request.Password))
            {
                throw new ArgumentException("Password is required.", nameof(request));
            }

            _ = await _authClient
                .LoginAsync(ToLoginRequest(request), cancellationToken)
                .ConfigureAwait(false);
            UserDto user = await _authClient.MeAsync(cancellationToken).ConfigureAwait(false);
            return ToSession(user);
        }

        /// <inheritdoc />
        public async Task<AuthSession> RestoreSessionAsync(CancellationToken cancellationToken = default)
        {
            UserDto user = await _authClient.MeAsync(cancellationToken).ConfigureAwait(false);
            return ToSession(user);
        }

        /// <inheritdoc />
        public Task SignOutAsync(CancellationToken cancellationToken = default)
        {
            return _authClient.LogoutAsync(cancellationToken: cancellationToken);
        }

        private static LoginRequestDto ToLoginRequest(PasswordSignInRequest request)
        {
            return new LoginRequestDto
            {
                Username = request.Username.Trim(),
                Password = request.Password,
                TwoFactorCode = NormalizeOptional(request.TwoFactorCode),
                TrustDevice = request.TrustDevice,
                FirstName = NormalizeOptional(request.FirstName),
                LastName = NormalizeOptional(request.LastName),
            };
        }

        private static AuthSession ToSession(UserDto user)
        {
            return new AuthSession(user.Id, user.Username, user.Email, user.IsTotpEnabled);
        }

        private static string? NormalizeOptional(string? value)
        {
            string? trimmed = value?.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }
    }
}
