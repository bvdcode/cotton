// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Reflection;

namespace Cotton.Server.IntegrationTests;

public class PreviewControllerConcurrencyTests
{
    [Test]
    public void PreviewController_ExplicitlyBypassesRateLimiting()
    {
        DisableRateLimitingAttribute? attribute =
            typeof(PreviewController).GetCustomAttribute<DisableRateLimitingAttribute>();

        Assert.That(attribute, Is.Not.Null);
    }

    [Test]
    [NonParallelizable]
    public async Task GetFilePreview_HoldsSemaphoreUntilResponseCompletes()
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
