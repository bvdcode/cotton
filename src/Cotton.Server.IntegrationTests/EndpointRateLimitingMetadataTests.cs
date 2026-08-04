// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Auth;
using Cotton.Server.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class EndpointRateLimitingMetadataTests
    {
        [Test]
        public void PublicShareArchive_UsesArchiveRateLimit()
        {
            IReadOnlyList<RouteEndpoint> endpoints = GetControllerEndpoints();
            RouteEndpoint archiveEndpoint = endpoints.Single(endpoint =>
                RouteMatches(
                    endpoint,
                    $"{Routes.V1.Layouts}/shared/{{token}}/archives/download-link")
                && endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
                    .Contains(HttpMethods.Post) == true);
            EnableRateLimitingAttribute? attribute =
                archiveEndpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>();

            Assert.That(attribute?.PolicyName, Is.EqualTo(AuthRateLimitPolicies.PublicShareArchive));
        }

        [Test]
        public void PreviewEndpoints_ExplicitlyBypassRateLimiting()
        {
            string[] previewRoutes =
            [
                $"{Routes.V1.Previews}/{{previewHashEncryptedHex}}",
                $"{Routes.V1.Previews}/{{previewHashEncryptedHex}}.webp",
            ];
            IReadOnlyList<RouteEndpoint> previewEndpoints = GetControllerEndpoints()
                .Where(endpoint => previewRoutes.Any(route => RouteMatches(endpoint, route)))
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(previewEndpoints, Has.Count.EqualTo(previewRoutes.Length));
                Assert.That(
                    previewEndpoints.All(endpoint =>
                        endpoint.Metadata.GetMetadata<DisableRateLimitingAttribute>() is not null),
                    Is.True);
            });
        }

        private static IReadOnlyList<RouteEndpoint> GetControllerEndpoints()
        {
            WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
            builder.Services
                .AddControllers()
                .AddApplicationPart(typeof(LayoutController).Assembly);

            using WebApplication app = builder.Build();
            app.MapControllers();
            IEndpointRouteBuilder routes = app;
            return routes.DataSources
                .SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>()
                .ToArray();
        }

        private static bool RouteMatches(RouteEndpoint endpoint, string route)
        {
            return string.Equals(
                endpoint.RoutePattern.RawText?.TrimStart('/'),
                route.TrimStart('/'),
                StringComparison.Ordinal);
        }
    }
}
