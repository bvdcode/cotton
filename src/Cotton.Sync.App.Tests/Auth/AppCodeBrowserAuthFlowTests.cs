// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Net.Http;
using Cotton.Auth;
using Cotton.Sdk.Auth;
using Cotton.Sync.App.Auth;
using Cotton.Sync.App.Platform;

namespace Cotton.Sync.App.Tests.Auth
{
    public class AppCodeBrowserAuthFlowTests
    {
        [Test]
        public async Task SignInAsync_StartsSessionOpensBrowserPollsAndReturnsSession()
        {
            Guid userId = Guid.NewGuid();
            var authClient = new FakeCottonAuthClient
            {
                CurrentUser = new UserDto
                {
                    Id = userId,
                    Username = "cotton",
                    Email = "cotton@example.test",
                    IsTotpEnabled = true,
                },
            };
            authClient.PollResults.Enqueue(new AppCodePollResult
            {
                Status = AppCodePollStatus.Approved,
                Tokens = new TokenPairDto { AccessToken = "access", RefreshToken = "refresh" },
            });
            var platformCommands = new FakePlatformCommandService();
            var flow = new AppCodeBrowserAuthFlow(authClient, platformCommands);

            AuthSession session = await flow.SignInAsync(new AppCodeBrowserSignInRequest
            {
                ApplicationName = " Cotton Sync Desktop ",
                ApplicationVersion = " 1.2.3 ",
                DeviceName = " workstation ",
            });

            Assert.Multiple(() =>
            {
                Assert.That(authClient.StartCallCount, Is.EqualTo(1));
                Assert.That(authClient.LastStartRequest?.ApplicationName, Is.EqualTo("Cotton Sync Desktop"));
                Assert.That(authClient.LastStartRequest?.ApplicationVersion, Is.EqualTo("1.2.3"));
                Assert.That(authClient.LastStartRequest?.DeviceName, Is.EqualTo("workstation"));
                Assert.That(platformCommands.OpenWebCallCount, Is.EqualTo(1));
                Assert.That(platformCommands.LastOpenedUrl, Is.EqualTo(authClient.Session.ApprovalUri));
                Assert.That(authClient.PollCallCount, Is.EqualTo(1));
                Assert.That(authClient.LastPollToken, Is.EqualTo(authClient.Session.PollToken));
                Assert.That(authClient.MeCallCount, Is.EqualTo(1));
                Assert.That(session.UserId, Is.EqualTo(userId));
                Assert.That(session.Email, Is.EqualTo("cotton@example.test"));
                Assert.That(session.IsTotpEnabled, Is.True);
            });
        }

        [Test]
        public async Task SignInAsync_WaitsForPendingPollAndContinues()
        {
            var delays = new List<TimeSpan>();
            var authClient = new FakeCottonAuthClient();
            authClient.PollResults.Enqueue(new AppCodePollResult
            {
                Status = AppCodePollStatus.Pending,
                Error = "pending",
                RetryAfter = TimeSpan.FromSeconds(7),
            });
            authClient.PollResults.Enqueue(new AppCodePollResult
            {
                Status = AppCodePollStatus.Approved,
                Tokens = new TokenPairDto { AccessToken = "access", RefreshToken = "refresh" },
            });
            var flow = new AppCodeBrowserAuthFlow(
                authClient,
                new FakePlatformCommandService(),
                (delay, _) =>
                {
                    delays.Add(delay);
                    return Task.CompletedTask;
                });

            await flow.SignInAsync(new AppCodeBrowserSignInRequest
            {
                ApplicationName = "Cotton Sync Desktop",
            });

            Assert.Multiple(() =>
            {
                Assert.That(authClient.PollCallCount, Is.EqualTo(2));
                Assert.That(delays, Is.EqualTo(new[] { TimeSpan.FromSeconds(7) }));
            });
        }

        [Test]
        public async Task SignInAsync_RetriesTransientPollFailureAndContinues()
        {
            var delays = new List<TimeSpan>();
            var authClient = new FakeCottonAuthClient();
            authClient.PollExceptions.Enqueue(new HttpRequestException("Temporary network failure."));
            authClient.PollResults.Enqueue(new AppCodePollResult
            {
                Status = AppCodePollStatus.Approved,
                Tokens = new TokenPairDto { AccessToken = "access", RefreshToken = "refresh" },
            });
            var flow = new AppCodeBrowserAuthFlow(
                authClient,
                new FakePlatformCommandService(),
                (delay, _) =>
                {
                    delays.Add(delay);
                    return Task.CompletedTask;
                });

            await flow.SignInAsync(new AppCodeBrowserSignInRequest
            {
                ApplicationName = "Cotton Sync Desktop",
            });

            Assert.Multiple(() =>
            {
                Assert.That(authClient.PollCallCount, Is.EqualTo(2));
                Assert.That(authClient.LastPollToken, Is.EqualTo(authClient.Session.PollToken));
                Assert.That(authClient.MeCallCount, Is.EqualTo(1));
                Assert.That(delays, Is.EqualTo(new[] { authClient.Session.PollInterval }));
            });
        }

        [Test]
        public async Task SignInAsync_RetriesTransientUserLookupFailureAfterApproval()
        {
            var delays = new List<TimeSpan>();
            var authClient = new FakeCottonAuthClient();
            authClient.PollResults.Enqueue(new AppCodePollResult
            {
                Status = AppCodePollStatus.Approved,
                Tokens = new TokenPairDto { AccessToken = "access", RefreshToken = "refresh" },
            });
            authClient.MeExceptions.Enqueue(new IOException("Temporary user lookup failure."));
            var flow = new AppCodeBrowserAuthFlow(
                authClient,
                new FakePlatformCommandService(),
                (delay, _) =>
                {
                    delays.Add(delay);
                    return Task.CompletedTask;
                });

            AuthSession session = await flow.SignInAsync(new AppCodeBrowserSignInRequest
            {
                ApplicationName = "Cotton Sync Desktop",
            });

            Assert.Multiple(() =>
            {
                Assert.That(authClient.PollCallCount, Is.EqualTo(1));
                Assert.That(authClient.MeCallCount, Is.EqualTo(2));
                Assert.That(session.Email, Is.EqualTo("browser@example.test"));
                Assert.That(delays, Is.EqualTo(new[] { authClient.Session.PollInterval }));
            });
        }

        [Test]
        public void SignInAsync_StopsTransientUserLookupRetriesWhenSessionExpires()
        {
            DateTime currentTime = DateTime.UtcNow;
            var delays = new List<TimeSpan>();
            var authClient = new FakeCottonAuthClient();
            authClient.Session.ExpiresAt = currentTime.AddSeconds(1);
            authClient.PollResults.Enqueue(new AppCodePollResult
            {
                Status = AppCodePollStatus.Approved,
                Tokens = new TokenPairDto { AccessToken = "access", RefreshToken = "refresh" },
            });
            authClient.MeExceptions.Enqueue(new IOException("Temporary user lookup failure."));
            authClient.MeExceptions.Enqueue(new IOException("Still unavailable."));
            var flow = new AppCodeBrowserAuthFlow(
                authClient,
                new FakePlatformCommandService(),
                (delay, _) =>
                {
                    delays.Add(delay);
                    currentTime = currentTime.Add(delay).AddMilliseconds(1);
                    return Task.CompletedTask;
                },
                utcNow: () => currentTime);

            AppCodeBrowserSignInException? exception = Assert.ThrowsAsync<AppCodeBrowserSignInException>(
                async () => await flow.SignInAsync(new AppCodeBrowserSignInRequest
                {
                    ApplicationName = "Cotton Sync Desktop",
                }));

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.Not.Null);
                Assert.That(exception!.Status, Is.EqualTo(AppCodePollStatus.Expired));
                Assert.That(exception.Error, Is.EqualTo("expired"));
                Assert.That(authClient.PollCallCount, Is.EqualTo(1));
                Assert.That(authClient.MeCallCount, Is.EqualTo(2));
                Assert.That(delays, Is.EqualTo(new[] { authClient.Session.PollInterval }));
            });
        }

        [Test]
        public void SignInAsync_StopsTransientPollRetriesWhenSessionExpires()
        {
            DateTime currentTime = DateTime.UtcNow;
            var delays = new List<TimeSpan>();
            var authClient = new FakeCottonAuthClient();
            authClient.Session.ExpiresAt = currentTime.AddSeconds(1);
            authClient.PollExceptions.Enqueue(new HttpRequestException("Temporary network failure."));
            var flow = new AppCodeBrowserAuthFlow(
                authClient,
                new FakePlatformCommandService(),
                (delay, _) =>
                {
                    delays.Add(delay);
                    currentTime = currentTime.Add(delay).AddMilliseconds(1);
                    return Task.CompletedTask;
                },
                utcNow: () => currentTime);

            AppCodeBrowserSignInException? exception = Assert.ThrowsAsync<AppCodeBrowserSignInException>(
                async () => await flow.SignInAsync(new AppCodeBrowserSignInRequest
                {
                    ApplicationName = "Cotton Sync Desktop",
                }));

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.Not.Null);
                Assert.That(exception!.Status, Is.EqualTo(AppCodePollStatus.Expired));
                Assert.That(exception.Error, Is.EqualTo("expired"));
                Assert.That(authClient.PollCallCount, Is.EqualTo(1));
                Assert.That(authClient.MeCallCount, Is.Zero);
                Assert.That(delays, Is.EqualTo(new[] { authClient.Session.PollInterval }));
            });
        }

        [TestCase(AppCodePollStatus.Denied, "denied", "Browser sign-in was denied.")]
        [TestCase(AppCodePollStatus.Expired, "expired", "Browser sign-in request expired.")]
        [TestCase(AppCodePollStatus.NotFound, "not_found", "Browser sign-in request was not found.")]
        public void SignInAsync_ThrowsForTerminalPollStatus(
            AppCodePollStatus status,
            string error,
            string message)
        {
            var authClient = new FakeCottonAuthClient();
            authClient.PollResults.Enqueue(new AppCodePollResult
            {
                Status = status,
                Error = error,
            });
            var flow = new AppCodeBrowserAuthFlow(authClient, new FakePlatformCommandService());

            AppCodeBrowserSignInException? exception = Assert.ThrowsAsync<AppCodeBrowserSignInException>(
                async () => await flow.SignInAsync(new AppCodeBrowserSignInRequest
                {
                    ApplicationName = "Cotton Sync Desktop",
                }));

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.Not.Null);
                Assert.That(exception!.Status, Is.EqualTo(status));
                Assert.That(exception.Error, Is.EqualTo(error));
                Assert.That(exception.Message, Is.EqualTo(message));
                Assert.That(authClient.MeCallCount, Is.Zero);
            });
        }

        [Test]
        public void SignInAsync_RejectsMissingApplicationNameBeforeSdkCall()
        {
            var authClient = new FakeCottonAuthClient();
            var flow = new AppCodeBrowserAuthFlow(authClient, new FakePlatformCommandService());

            ArgumentException? exception = Assert.ThrowsAsync<ArgumentException>(
                async () => await flow.SignInAsync(new AppCodeBrowserSignInRequest
                {
                    ApplicationName = " ",
                }));

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.Not.Null);
                Assert.That(exception!.ParamName, Is.EqualTo("request"));
                Assert.That(authClient.StartCallCount, Is.Zero);
            });
        }

        private class FakeCottonAuthClient : ICottonAuthClient
        {
            public AppCodeAuthorizationSession Session { get; } = new()
            {
                ApprovalId = Guid.Parse("0190a000-0000-7000-8000-000000000011"),
                ApprovalUri = new Uri("https://cotton.test/oauth/app-code/0190a000-0000-7000-8000-000000000011"),
                PollToken = "poll-token",
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                PollInterval = TimeSpan.FromSeconds(2),
            };

            public Queue<Exception> PollExceptions { get; } = new();

            public Queue<AppCodePollResult> PollResults { get; } = new();

            public Queue<Exception> MeExceptions { get; } = new();

            public int StartCallCount { get; private set; }

            public int PollCallCount { get; private set; }

            public int MeCallCount { get; private set; }

            public AppCodeStartRequestDto? LastStartRequest { get; private set; }

            public string? LastPollToken { get; private set; }

            public UserDto CurrentUser { get; set; } = new()
            {
                Id = Guid.NewGuid(),
                Username = "browser",
                Email = "browser@example.test",
            };

            public Task<TokenPairDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<AppCodeAuthorizationSession> StartAppCodeAsync(
                AppCodeStartRequestDto request,
                CancellationToken cancellationToken = default)
            {
                StartCallCount++;
                LastStartRequest = request;
                return Task.FromResult(Session);
            }

            public Task<AppCodePollResult> PollAppCodeAsync(
                string pollToken,
                CancellationToken cancellationToken = default)
            {
                PollCallCount++;
                LastPollToken = pollToken;
                if (PollExceptions.TryDequeue(out Exception? exception))
                {
                    throw exception;
                }

                return Task.FromResult(PollResults.Dequeue());
            }

            public Task<TokenPairDto> RefreshAsync(
                string? refreshToken = null,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task LogoutAsync(string? refreshToken = null, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<UserDto> MeAsync(CancellationToken cancellationToken = default)
            {
                MeCallCount++;
                if (MeExceptions.TryDequeue(out Exception? exception))
                {
                    throw exception;
                }

                return Task.FromResult(CurrentUser);
            }
        }

        private class FakePlatformCommandService : IPlatformCommandService
        {
            public int OpenWebCallCount { get; private set; }

            public Uri? LastOpenedUrl { get; private set; }

            public Task OpenFolderAsync(string localPath, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task OpenWebAsync(Uri url, CancellationToken cancellationToken = default)
            {
                OpenWebCallCount++;
                LastOpenedUrl = url;
                return Task.CompletedTask;
            }
        }
    }
}
