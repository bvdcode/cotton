// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Sync.App.Auth;
using Cotton.Sdk.Auth;

namespace Cotton.Sync.App.Tests.Auth
{
    public sealed class PasswordAuthFlowTests
    {
        [Test]
        public async Task SignInAsync_MapsRequestAndReturnsSession()
        {
            Guid userId = Guid.NewGuid();
            var authClient = new FakeCottonAuthClient
            {
                CurrentUser = new UserDto
                {
                    Id = userId,
                    Username = "vadim",
                    Email = "vadim@example.test",
                    IsTotpEnabled = true,
                },
            };
            var flow = new PasswordAuthFlow(authClient);

            AuthSession session = await flow.SignInAsync(new PasswordSignInRequest
            {
                Username = " vadim ",
                Password = "  keep password spaces  ",
                TwoFactorCode = " 123456 ",
                TrustDevice = true,
                FirstName = " Vadim ",
                LastName = " ",
            });

            Assert.Multiple(() =>
            {
                Assert.That(authClient.LoginCallCount, Is.EqualTo(1));
                Assert.That(authClient.LastLoginRequest, Is.Not.Null);
                Assert.That(authClient.LastLoginRequest!.Username, Is.EqualTo("vadim"));
                Assert.That(authClient.LastLoginRequest.Password, Is.EqualTo("  keep password spaces  "));
                Assert.That(authClient.LastLoginRequest.TwoFactorCode, Is.EqualTo("123456"));
                Assert.That(authClient.LastLoginRequest.TrustDevice, Is.True);
                Assert.That(authClient.LastLoginRequest.FirstName, Is.EqualTo("Vadim"));
                Assert.That(authClient.LastLoginRequest.LastName, Is.Null);
                Assert.That(session.UserId, Is.EqualTo(userId));
                Assert.That(session.Username, Is.EqualTo("vadim"));
                Assert.That(session.Email, Is.EqualTo("vadim@example.test"));
                Assert.That(session.IsTotpEnabled, Is.True);
            });
        }

        [Test]
        public async Task SignInAsync_RejectsMissingUsernameBeforeSdkCall()
        {
            var authClient = new FakeCottonAuthClient();
            var flow = new PasswordAuthFlow(authClient);

            ArgumentException? exception = Assert.ThrowsAsync<ArgumentException>(
                async () => await flow.SignInAsync(new PasswordSignInRequest
                {
                    Username = " ",
                    Password = "password",
                }));

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.Not.Null);
                Assert.That(exception!.ParamName, Is.EqualTo("request"));
                Assert.That(authClient.LoginCallCount, Is.Zero);
            });
        }

        [Test]
        public async Task SignInAsync_RejectsMissingPasswordBeforeSdkCall()
        {
            var authClient = new FakeCottonAuthClient();
            var flow = new PasswordAuthFlow(authClient);

            ArgumentException? exception = Assert.ThrowsAsync<ArgumentException>(
                async () => await flow.SignInAsync(new PasswordSignInRequest
                {
                    Username = "vadim",
                    Password = string.Empty,
                }));

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.Not.Null);
                Assert.That(exception!.ParamName, Is.EqualTo("request"));
                Assert.That(authClient.LoginCallCount, Is.Zero);
            });
        }

        [Test]
        public async Task SignOutAsync_DelegatesToSdkLogout()
        {
            var authClient = new FakeCottonAuthClient();
            var flow = new PasswordAuthFlow(authClient);

            await flow.SignOutAsync();

            Assert.Multiple(() =>
            {
                Assert.That(authClient.LogoutCallCount, Is.EqualTo(1));
                Assert.That(authClient.LastLogoutRefreshToken, Is.Null);
            });
        }

        [Test]
        public async Task RestoreSessionAsync_ReturnsCurrentSdkUser()
        {
            Guid userId = Guid.NewGuid();
            var authClient = new FakeCottonAuthClient
            {
                CurrentUser = new UserDto
                {
                    Id = userId,
                    Username = "restored",
                    Email = "restored@example.test",
                    IsTotpEnabled = false,
                },
            };
            var flow = new PasswordAuthFlow(authClient);

            AuthSession session = await flow.RestoreSessionAsync();

            Assert.Multiple(() =>
            {
                Assert.That(authClient.MeCallCount, Is.EqualTo(1));
                Assert.That(session.UserId, Is.EqualTo(userId));
                Assert.That(session.Username, Is.EqualTo("restored"));
                Assert.That(session.Email, Is.EqualTo("restored@example.test"));
            });
        }

        private sealed class FakeCottonAuthClient : ICottonAuthClient
        {
            public int LoginCallCount { get; private set; }

            public int LogoutCallCount { get; private set; }

            public int MeCallCount { get; private set; }

            public LoginRequestDto? LastLoginRequest { get; private set; }

            public string? LastLogoutRefreshToken { get; private set; }

            public UserDto CurrentUser { get; set; } = new()
            {
                Id = Guid.NewGuid(),
                Username = "user",
            };

            public Task<TokenPairDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
            {
                LoginCallCount++;
                LastLoginRequest = request;
                return Task.FromResult(new TokenPairDto
                {
                    AccessToken = "access-token",
                    RefreshToken = "refresh-token",
                });
            }

            public Task<AppCodeAuthorizationSession> StartAppCodeAsync(
                AppCodeStartRequestDto request,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<AppCodePollResult> PollAppCodeAsync(
                string pollToken,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<TokenPairDto> RefreshAsync(
                string? refreshToken = null,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new TokenPairDto
                {
                    AccessToken = "refreshed-access-token",
                    RefreshToken = refreshToken ?? "refreshed-refresh-token",
                });
            }

            public Task LogoutAsync(string? refreshToken = null, CancellationToken cancellationToken = default)
            {
                LogoutCallCount++;
                LastLogoutRefreshToken = refreshToken;
                return Task.CompletedTask;
            }

            public Task<UserDto> MeAsync(CancellationToken cancellationToken = default)
            {
                MeCallCount++;
                return Task.FromResult(CurrentUser);
            }
        }
    }
}
