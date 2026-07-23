// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Controllers;
using Cotton.Server.Extensions;
using Cotton.Server.Models;
using Cotton.Server.Models.Configuration;
using Cotton.Server.Services.RequestAdmission;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace Cotton.Server.IntegrationTests
{
    public class HttpRequestAdmissionPolicyTests
    {
        [Test]
        public void PreviewController_DisablesRateLimitingInFavorOfItsSemaphoreQueue()
        {
            DisableRateLimitingAttribute? attribute =
                typeof(PreviewController).GetCustomAttribute<DisableRateLimitingAttribute>();

            Assert.That(attribute, Is.Not.Null);
        }

        [Test]
        [NonParallelizable]
        public async Task PreviewController_HoldsSemaphoreUntilResponseCompletes()
        {
            List<CapturingResponseFeature> responseFeatures = [];
            Task<IActionResult>? queuedRequest = null;
            CapturingResponseFeature? queuedResponseFeature = null;

            try
            {
                for (int i = 0; i < PreviewController.PreviewConcurrencyLimit; i++)
                {
                    (PreviewController controller, CapturingResponseFeature feature) =
                        CreatePreviewController();
                    responseFeatures.Add(feature);

                    IActionResult result = await controller.GetFilePreview("invalid");
                    Assert.That(
                        (result as ObjectResult)?.StatusCode,
                        Is.EqualTo(StatusCodes.Status404NotFound));
                }

                (PreviewController queuedController, CapturingResponseFeature queuedFeature) =
                    CreatePreviewController();
                responseFeatures.Add(queuedFeature);
                queuedResponseFeature = queuedFeature;
                queuedRequest = queuedController.GetFilePreview("invalid");
                Assert.That(queuedRequest.IsCompleted, Is.False);

                await responseFeatures[0].CompleteAsync();

                IActionResult admittedResult =
                    await queuedRequest.WaitAsync(TimeSpan.FromSeconds(1));
                Assert.That(
                    (admittedResult as ObjectResult)?.StatusCode,
                    Is.EqualTo(StatusCodes.Status404NotFound));
            }
            finally
            {
                foreach (CapturingResponseFeature feature in responseFeatures
                    .Where(feature => feature != queuedResponseFeature))
                {
                    await feature.CompleteAsync();
                }

                try
                {
                    if (queuedRequest is not null)
                    {
                        await queuedRequest.WaitAsync(TimeSpan.FromSeconds(1));
                    }
                }
                finally
                {
                    if (queuedResponseFeature is not null)
                    {
                        await queuedResponseFeature.CompleteAsync();
                    }
                }
            }
        }

        [Test]
        public async Task Create_QueuesPerClientBurstUntilCapacityIsReleased()
        {
            RequestAdmissionOptions options = new()
            {
                GlobalConcurrentRequestLimit = 4,
                GlobalQueueLimit = 4,
                ClientConcurrentRequestLimit = 1,
                ClientQueueLimit = 1,
            };
            await using PartitionedRateLimiter<HttpContext> limiter = HttpRequestAdmissionPolicy.Create(options);
            DefaultHttpContext firstContext = CreateAuthenticatedContext("user-1");
            DefaultHttpContext secondContext = CreateAuthenticatedContext("user-1");
            DefaultHttpContext otherUserContext = CreateAuthenticatedContext("user-2");

            Task<RateLimitLease> queuedRequest;
            using (RateLimitLease first = await limiter.AcquireAsync(firstContext))
            {
                queuedRequest = limiter.AcquireAsync(secondContext).AsTask();
                using RateLimitLease otherUser = await limiter.AcquireAsync(otherUserContext);

                Assert.Multiple(() =>
                {
                    Assert.That(first.IsAcquired, Is.True);
                    Assert.That(queuedRequest.IsCompleted, Is.False);
                    Assert.That(otherUser.IsAcquired, Is.True);
                });
            }

            using RateLimitLease admitted = await queuedRequest.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.That(admitted.IsAcquired, Is.True);
        }

        [Test]
        public async Task Create_QueuesGlobalBurstUntilCapacityIsReleased()
        {
            RequestAdmissionOptions options = new()
            {
                GlobalConcurrentRequestLimit = 1,
                GlobalQueueLimit = 1,
                ClientConcurrentRequestLimit = 1,
                ClientQueueLimit = 1,
            };
            await using PartitionedRateLimiter<HttpContext> limiter = HttpRequestAdmissionPolicy.Create(options);

            Task<RateLimitLease> queuedRequest;
            using (RateLimitLease first = await limiter.AcquireAsync(CreateAuthenticatedContext("user-1")))
            {
                queuedRequest = limiter.AcquireAsync(CreateAuthenticatedContext("user-2")).AsTask();
                using RateLimitLease rejected =
                    await limiter.AcquireAsync(CreateAuthenticatedContext("user-3"));

                Assert.Multiple(() =>
                {
                    Assert.That(first.IsAcquired, Is.True);
                    Assert.That(queuedRequest.IsCompleted, Is.False);
                    Assert.That(rejected.IsAcquired, Is.False);
                });
            }

            using RateLimitLease admitted = await queuedRequest.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.That(admitted.IsAcquired, Is.True);
        }

        [Test]
        public async Task Create_RejectsRequestsOnlyAfterPerClientQueueIsFull()
        {
            RequestAdmissionOptions options = new()
            {
                GlobalConcurrentRequestLimit = 4,
                GlobalQueueLimit = 4,
                ClientConcurrentRequestLimit = 1,
                ClientQueueLimit = 1,
            };
            await using PartitionedRateLimiter<HttpContext> limiter = HttpRequestAdmissionPolicy.Create(options);
            DefaultHttpContext context = CreateAuthenticatedContext("user-1");

            Task<RateLimitLease> queuedRequest;
            using (RateLimitLease first = await limiter.AcquireAsync(context))
            {
                queuedRequest = limiter.AcquireAsync(CreateAuthenticatedContext("user-1")).AsTask();
                using RateLimitLease rejected =
                    await limiter.AcquireAsync(CreateAuthenticatedContext("user-1"));

                Assert.Multiple(() =>
                {
                    Assert.That(first.IsAcquired, Is.True);
                    Assert.That(queuedRequest.IsCompleted, Is.False);
                    Assert.That(rejected.IsAcquired, Is.False);
                });
            }

            using RateLimitLease admitted = await queuedRequest.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.That(admitted.IsAcquired, Is.True);
        }

        [Test]
        public async Task Create_CancelledQueuedRequestDoesNotConsumeReleasedCapacity()
        {
            RequestAdmissionOptions options = new()
            {
                GlobalConcurrentRequestLimit = 2,
                GlobalQueueLimit = 2,
                ClientConcurrentRequestLimit = 1,
                ClientQueueLimit = 1,
            };
            await using PartitionedRateLimiter<HttpContext> limiter = HttpRequestAdmissionPolicy.Create(options);
            DefaultHttpContext context = CreateAuthenticatedContext("user-1");
            using CancellationTokenSource cancellation = new();

            using (RateLimitLease first = await limiter.AcquireAsync(context))
            {
                Task<RateLimitLease> cancelledRequest = limiter.AcquireAsync(
                    CreateAuthenticatedContext("user-1"),
                    cancellationToken: cancellation.Token).AsTask();
                cancellation.Cancel();

                Assert.CatchAsync<OperationCanceledException>(
                    async () => await cancelledRequest.WaitAsync(TimeSpan.FromSeconds(1)));
            }

            using RateLimitLease admitted =
                await limiter.AcquireAsync(CreateAuthenticatedContext("user-1"))
                    .AsTask()
                    .WaitAsync(TimeSpan.FromSeconds(1));
            Assert.That(admitted.IsAcquired, Is.True);
        }

        [Test]
        public async Task Create_RejectsAnonymousRequestsAbovePerClientLimitForSameRemoteAddress()
        {
            RequestAdmissionOptions options = new()
            {
                GlobalConcurrentRequestLimit = 2,
                GlobalQueueLimit = 0,
                ClientConcurrentRequestLimit = 1,
                ClientQueueLimit = 0,
            };
            await using PartitionedRateLimiter<HttpContext> limiter = HttpRequestAdmissionPolicy.Create(options);
            DefaultHttpContext firstContext = CreateAnonymousContext("192.0.2.10");
            DefaultHttpContext secondContext = CreateAnonymousContext("192.0.2.10");
            DefaultHttpContext otherClientContext = CreateAnonymousContext("198.51.100.10");

            using RateLimitLease first = await limiter.AcquireAsync(firstContext);
            using RateLimitLease rejected = await limiter.AcquireAsync(secondContext);
            using RateLimitLease otherClient = await limiter.AcquireAsync(otherClientContext);

            Assert.Multiple(() =>
            {
                Assert.That(first.IsAcquired, Is.True);
                Assert.That(rejected.IsAcquired, Is.False);
                Assert.That(otherClient.IsAcquired, Is.True);
            });
        }

        [Test]
        public async Task Create_RejectsAnonymousRequestsAboveGlobalLimitAcrossRemoteAddresses()
        {
            RequestAdmissionOptions options = new()
            {
                GlobalConcurrentRequestLimit = 2,
                GlobalQueueLimit = 0,
                ClientConcurrentRequestLimit = 1,
                ClientQueueLimit = 0,
            };
            await using PartitionedRateLimiter<HttpContext> limiter = HttpRequestAdmissionPolicy.Create(options);

            using RateLimitLease first = await limiter.AcquireAsync(CreateAnonymousContext("192.0.2.10"));
            using RateLimitLease second = await limiter.AcquireAsync(CreateAnonymousContext("198.51.100.10"));
            using RateLimitLease rejected = await limiter.AcquireAsync(CreateAnonymousContext("203.0.113.10"));

            Assert.Multiple(() =>
            {
                Assert.That(first.IsAcquired, Is.True);
                Assert.That(second.IsAcquired, Is.True);
                Assert.That(rejected.IsAcquired, Is.False);
            });
        }

        [Test]
        public async Task Create_NormalizesIpv4MappedAnonymousRemoteAddress()
        {
            RequestAdmissionOptions options = new()
            {
                GlobalConcurrentRequestLimit = 2,
                GlobalQueueLimit = 0,
                ClientConcurrentRequestLimit = 1,
                ClientQueueLimit = 0,
            };
            await using PartitionedRateLimiter<HttpContext> limiter = HttpRequestAdmissionPolicy.Create(options);
            DefaultHttpContext ipv4Context = CreateAnonymousContext("192.0.2.10");
            DefaultHttpContext ipv4MappedContext = CreateAnonymousContext("::ffff:192.0.2.10");

            using RateLimitLease first = await limiter.AcquireAsync(ipv4Context);
            using RateLimitLease rejected = await limiter.AcquireAsync(ipv4MappedContext);

            Assert.Multiple(() =>
            {
                Assert.That(first.IsAcquired, Is.True);
                Assert.That(rejected.IsAcquired, Is.False);
            });
        }

        [Test]
        public async Task Create_WebDavBasicRequestsWithoutDefaultAuthenticationUseRemoteAddressLimit()
        {
            RequestAdmissionOptions options = new()
            {
                GlobalConcurrentRequestLimit = 2,
                GlobalQueueLimit = 0,
                ClientConcurrentRequestLimit = 1,
                ClientQueueLimit = 0,
            };
            await using PartitionedRateLimiter<HttpContext> limiter = HttpRequestAdmissionPolicy.Create(options);
            DefaultHttpContext firstContext = CreateWebDavBasicContext("192.0.2.10");
            DefaultHttpContext secondContext = CreateWebDavBasicContext("192.0.2.10");

            using RateLimitLease first = await limiter.AcquireAsync(firstContext);
            using RateLimitLease rejected = await limiter.AcquireAsync(secondContext);

            Assert.Multiple(() =>
            {
                Assert.That(first.IsAcquired, Is.True);
                Assert.That(rejected.IsAcquired, Is.False);
            });
        }

        [Test]
        public async Task CapacityRejection_ReturnsServiceUnavailable()
        {
            using ConcurrencyLimiter limiter = new(new ConcurrencyLimiterOptions
            {
                PermitLimit = 1,
                QueueLimit = 0,
            });
            using RateLimitLease held = await limiter.AcquireAsync(1);
            using RateLimitLease rejected = await limiter.AcquireAsync(1);
            DefaultHttpContext httpContext = new();
            httpContext.Response.Body = new MemoryStream();
            OnRejectedContext rejectedContext = new()
            {
                HttpContext = httpContext,
                Lease = rejected,
            };

            await RequestAdmissionExtensions.WriteCapacityRejectionAsync(
                rejectedContext,
                CancellationToken.None);

            httpContext.Response.Body.Position = 0;
            CottonResult? result = await System.Text.Json.JsonSerializer.DeserializeAsync<CottonResult>(
                httpContext.Response.Body,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            Assert.Multiple(() =>
            {
                Assert.That(httpContext.Response.StatusCode, Is.EqualTo(StatusCodes.Status503ServiceUnavailable));
                Assert.That(httpContext.Response.Headers.RetryAfter.ToString(), Is.EqualTo("1"));
                Assert.That(result?.MessageCode, Is.EqualTo("request_capacity_exhausted"));
                Assert.That(result?.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            });
        }

        private static DefaultHttpContext CreateAuthenticatedContext(string userId)
        {
            DefaultHttpContext context = new();
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId)],
                "test"));
            return context;
        }

        private static DefaultHttpContext CreateAnonymousContext(string remoteIpAddress)
        {
            return CreateAnonymousContext(IPAddress.Parse(remoteIpAddress));
        }

        private static DefaultHttpContext CreateAnonymousContext(IPAddress remoteIpAddress)
        {
            DefaultHttpContext context = new();
            context.Connection.RemoteIpAddress = remoteIpAddress;
            return context;
        }

        private static DefaultHttpContext CreateWebDavBasicContext(string remoteIpAddress)
        {
            DefaultHttpContext context = CreateAnonymousContext(remoteIpAddress);
            context.Request.Headers.Authorization = "Basic dXNlcjp0b2tlbg==";
            return context;
        }

        private static (PreviewController Controller, CapturingResponseFeature ResponseFeature)
            CreatePreviewController()
        {
            DefaultHttpContext context = new();
            var capturingFeature = new CapturingResponseFeature();
            context.Features.Set<IHttpResponseFeature>(capturingFeature);

            var controller = new PreviewController(
                null!,
                NullLogger<PreviewController>.Instance,
                null!)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = context,
                },
            };
            return (controller, capturingFeature);
        }

        private sealed class CapturingResponseFeature : HttpResponseFeature
        {
            private readonly object _completionLock = new();
            private readonly List<(Func<object, Task> Callback, object State)> _completed = [];
            private bool _completionInvoked;

            public override void OnCompleted(Func<object, Task> callback, object state)
            {
                bool invokeImmediately;
                lock (_completionLock)
                {
                    invokeImmediately = _completionInvoked;
                    if (!invokeImmediately)
                    {
                        _completed.Add((callback, state));
                    }
                }

                if (invokeImmediately)
                {
                    callback(state).GetAwaiter().GetResult();
                }
            }

            public async Task CompleteAsync()
            {
                (Func<object, Task> Callback, object State)[] callbacks;
                lock (_completionLock)
                {
                    if (_completionInvoked)
                    {
                        return;
                    }

                    _completionInvoked = true;
                    callbacks = [.. _completed];
                }

                for (int i = callbacks.Length - 1; i >= 0; i--)
                {
                    (Func<object, Task> callback, object state) = callbacks[i];
                    await callback(state);
                }
            }
        }
    }
}
