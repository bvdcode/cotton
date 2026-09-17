// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Handlers.Computation;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Models.Computation;
using Cotton.Server.Providers;
using Cotton.Server.Services.Computation;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Net;

namespace Cotton.Server.IntegrationTests
{
    public class ComputationServiceTests
    {
        private ServiceProvider _provider = null!;
        private ComputationService _service = null!;
        private TeiTestHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _handler = new TeiTestHandler();
            ServerSettingsCache cache = new();
            cache.GetOrAdd(() => ServerSettingsSnapshot.FromEntity(new CottonServerSettings
            {
                ComputionMode = ComputionMode.Remote,
                RemoteComputationRunnerUrl = "https://runner.example/proxy",
            }));
            ServiceCollection services = new();
            services.AddLogging();
            services.AddMediator();
            services.AddSingleton(cache);
            services.AddSingleton<EmbeddingDimensionCache>();
            services.AddSingleton(_ => new HttpClient(_handler));
            services.AddSingleton<TeiClient>();
            services.AddDbContext<CottonDbContext>(options => options.UseNpgsql());
            services.AddScoped<SettingsProvider>();
            services.AddTransient<ComputationService>();
            services.AddTransient<IRequestHandler<GetComputationServiceInfoQuery, ComputationServiceInfo>, GetComputationServiceInfoQueryHandler>();
            services.AddTransient<IRequestHandler<GetComputationStatusQuery, ComputationStatus>, GetComputationStatusQueryHandler>();
            services.AddTransient<IRequestHandler<GetTextEmbeddingsRequest, float[][]>, GetTextEmbeddingsRequestHandler>();
            _provider = services.BuildServiceProvider();
            _service = _provider.GetRequiredService<ComputationService>();
        }

        [TearDown]
        public async Task TearDown()
        {
            await _provider.DisposeAsync();
        }

        [Test]
        public async Task RepeatedStatus_RefreshesInfo_ButReusesValidatedDimensions()
        {
            ComputationStatus first = await _service.GetStatusAsync();
            ComputationStatus second = await _service.GetStatusAsync();
            Assert.Multiple(() =>
            {
                Assert.That(first.IsReady, Is.True);
                Assert.That(second.Dimensions, Is.EqualTo(1024));
                Assert.That(second.Info!.MaxInputTokens, Is.EqualTo(8192));
                Assert.That(_handler.InfoCalls, Is.EqualTo(2));
                Assert.That(_handler.EmbedCalls, Is.EqualTo(1));
                Assert.That(_handler.Addresses.All(x => x.AbsolutePath.StartsWith("/proxy/", StringComparison.Ordinal)), Is.True);
            });
        }

        [Test]
        public async Task ExplicitValidation_AlwaysProbes_AndFailureIsNotCached()
        {
            await _service.GetStatusAsync();
            _handler.Dimensions = 768;
            ComputationStatus invalid = await _service.GetStatusAsync(forceRefresh: true);
            _handler.Dimensions = 1024;
            ComputationStatus recovered = await _service.GetStatusAsync();
            Assert.Multiple(() =>
            {
                Assert.That(invalid.Error, Is.EqualTo(ComputationError.InvalidDimensions));
                Assert.That(recovered.IsReady, Is.True);
                Assert.That(_handler.EmbedCalls, Is.EqualTo(3));
            });
        }

        [Test]
        public async Task ChangedEndpointOrModelRevision_RequiresANewProbe()
        {
            await _service.GetStatusAsync();
            _handler.ModelRevision = "new-revision";
            await _service.GetStatusAsync();
            await _service.GetStatusAsync("https://another.example");
            Assert.That(_handler.EmbedCalls, Is.EqualTo(3));
        }

        [Test]
        public async Task ConcurrentStatus_OnlyProbesOnce()
        {
            ComputationStatus[] statuses = await Task.WhenAll(
                Enumerable.Range(0, 5).Select(_ => _service.GetStatusAsync()));
            Assert.That(statuses.All(x => x.IsReady), Is.True);
            Assert.That(_handler.EmbedCalls, Is.EqualTo(1));
        }

        [TestCase(false, "BAAI/bge-m3", ComputationError.NotEmbeddingModel)]
        [TestCase(true, "another/1024-model", ComputationError.IncompatibleModel)]
        public async Task IncompatibleInfo_IsRejectedBeforeInference(bool embedding, string model, ComputationError error)
        {
            _handler.IsEmbeddingModel = embedding;
            _handler.ModelId = model;
            ComputationStatus status = await _service.GetStatusAsync();
            Assert.That(status.Error, Is.EqualTo(error));
            Assert.That(_handler.EmbedCalls, Is.Zero);
        }

        [Test]
        public async Task Info_DoesNotRunInference()
        {
            ComputationServiceInfo info = await _service.GetServiceInfoAsync();
            Assert.That(info.MaxBatchInputs, Is.EqualTo(32));
            Assert.That(_handler.EmbedCalls, Is.Zero);
        }

        [Test]
        public async Task Embeddings_SplitAtClientLimit_AndPreserveOrder()
        {
            _handler.MaxBatchInputs = 2;
            string[] texts = ["one", "four", "three", "xx", "a"];
            float[][] vectors = await _service.GetTextEmbeddingsAsync(texts);
            Assert.Multiple(() =>
            {
                Assert.That(_handler.Batches.Select(x => x.Length), Is.EqualTo(new[] { 2, 2, 1 }));
                Assert.That(vectors.Select(x => x[0]), Is.EqualTo(texts.Select(x => (float)x.Length)));
                Assert.That(vectors.All(x => x.Length == 1024), Is.True);
                Assert.That(_handler.Truncate, Is.False);
                Assert.That(_handler.Normalize, Is.True);
            });
        }

        [TestCase(768, 0, false, ComputationError.InvalidDimensions)]
        [TestCase(1024, -1, false, ComputationError.InvalidVectors)]
        [TestCase(1024, 0, true, ComputationError.InvalidVectors)]
        public void Embeddings_ValidateEveryResponse(int dimensions, int offset, bool zero, ComputationError error)
        {
            _handler.Dimensions = dimensions;
            _handler.CountOffset = offset;
            _handler.ZeroVectors = zero;
            ComputationException? exception = Assert.ThrowsAsync<ComputationException>(
                async () => await _service.GetTextEmbeddingsAsync(["text"]));
            Assert.That(exception!.Error, Is.EqualTo(error));
        }

        [Test]
        public async Task HttpFailure_IsReportedAsUnavailable()
        {
            _handler.StatusCode = HttpStatusCode.ServiceUnavailable;
            Assert.That((await _service.GetStatusAsync()).Error, Is.EqualTo(ComputationError.Unreachable));
        }

        [Test]
        public async Task InvalidJson_IsReportedAsInvalidResponse()
        {
            _handler.InvalidJson = true;
            Assert.That((await _service.GetStatusAsync()).Error, Is.EqualTo(ComputationError.InvalidResponse));
        }

        [Test]
        public void CallerCancellation_IsPropagated()
        {
            using CancellationTokenSource source = new();
            source.Cancel();
            Assert.CatchAsync<OperationCanceledException>(
                async () => await _service.GetStatusAsync(cancellationToken: source.Token));
        }
    }
}
