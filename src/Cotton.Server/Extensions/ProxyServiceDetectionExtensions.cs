// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Extensions
{
    /// <summary>
    /// Detects informational reverse-proxy product hints from a safe allowlist of request-header names.
    /// </summary>
    public static class ProxyServiceDetectionExtensions
    {
        internal const string CloudflareService = "cloudflare";
        internal const string CloudFrontService = "cloudfront";
        internal const string AzureFrontDoorService = "azure-front-door";
        internal const string FastlyService = "fastly";
        internal const string FlyIoService = "fly-io";
        internal const string VercelService = "vercel";
        internal const string AwsAlbService = "aws-alb";
        internal const string TraefikService = "traefik";
        internal const string EnvoyService = "envoy";
        internal const string GenericReverseProxyService = "reverse-proxy";
        internal const string DeclaredProxyHeaderName = "X-Cotton-Proxy";

        private static readonly HashSet<string> DeclaredServiceNames = new(StringComparer.OrdinalIgnoreCase)
        {
            CloudflareService,
            CloudFrontService,
            AzureFrontDoorService,
            FastlyService,
            FlyIoService,
            VercelService,
            AwsAlbService,
            TraefikService,
            EnvoyService,
            "nginx",
            "caddy",
            "haproxy",
            "apache",
        };

        private static readonly HashSet<string> LocalProxyServiceNames = new(StringComparer.Ordinal)
        {
            TraefikService,
            EnvoyService,
            "nginx",
            "caddy",
            "haproxy",
            "apache",
        };

        private static readonly HashSet<string> EdgeProxyServiceNames = new(StringComparer.Ordinal)
        {
            CloudflareService,
            CloudFrontService,
            AzureFrontDoorService,
            FastlyService,
            FlyIoService,
            VercelService,
            AwsAlbService,
        };

        private static readonly string[] ForwardingHeaderNames =
        [
            "Forwarded",
            "X-Forwarded-For",
            "X-Forwarded-Host",
            "X-Forwarded-Port",
            "X-Forwarded-Proto",
            "X-Forwarded-Server",
            "X-Real-IP",
        ];

        private static readonly (string Marker, string Service)[] ServerHeaderSignatures =
        [
            ("cloudflare", CloudflareService),
            ("cloudfront", CloudFrontService),
            ("fastly", FastlyService),
            ("fly.io", FlyIoService),
            ("vercel", VercelService),
            ("awselb", AwsAlbService),
            ("traefik", TraefikService),
            ("envoy", EnvoyService),
            ("nginx", "nginx"),
            ("caddy", "caddy"),
            ("haproxy", "haproxy"),
            ("apache", "apache"),
        ];

        /// <summary>
        /// Returns stable service identifiers inferred from the current request. These hints are informational only
        /// and must not be used to establish proxy trust. A local proxy without a distinctive native header may
        /// overwrite X-Cotton-Proxy with one or more allowlisted service identifiers in public-to-Cotton order.
        /// </summary>
        public static IReadOnlyList<string> DetectProxyServices(this HttpRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            List<string> services = [];
            if (HasHeader(request, "CF-Ray"))
            {
                AddOnce(services, CloudflareService);
            }

            if (HasHeader(request, "X-Amz-Cf-Id"))
            {
                AddOnce(services, CloudFrontService);
            }

            if (HasHeader(request, "X-Azure-FDID"))
            {
                AddOnce(services, AzureFrontDoorService);
            }

            if (HasHeader(request, "Fastly-Client-IP"))
            {
                AddOnce(services, FastlyService);
            }

            if (HasHeader(request, "Fly-Client-IP"))
            {
                AddOnce(services, FlyIoService);
            }

            if (HasHeader(request, "X-Vercel-Id"))
            {
                AddOnce(services, VercelService);
            }

            if (HasHeader(request, "X-Amzn-Trace-Id"))
            {
                AddOnce(services, AwsAlbService);
            }

            if (HasHeader(request, "X-Envoy-External-Address"))
            {
                AddOnce(services, EnvoyService);
            }

            AddDeclaredServices(request, services);

            bool hasNamedLocalProxy = services.Any(LocalProxyServiceNames.Contains);
            bool hasForwardingHeaders = ForwardingHeaderNames.Any(name => HasHeader(request, name));
            bool hasUnidentifiedImmediateProxy = HasHeader(request, "X-Forwarded-Server");
            if (!hasNamedLocalProxy
                && ((services.Count == 0 && hasForwardingHeaders) || hasUnidentifiedImmediateProxy))
            {
                services.Add(GenericReverseProxyService);
            }

            return services;
        }

        /// <summary>
        /// Returns stable service identifiers inferred from proxy-added response headers.
        /// </summary>
        public static IReadOnlyList<string> DetectProxyServices(HttpResponseMessage response)
        {
            ArgumentNullException.ThrowIfNull(response);

            List<string> services = [];
            if (HasHeader(response, "CF-Ray")) AddOnce(services, CloudflareService);
            if (HasHeader(response, "X-Amz-Cf-Id") || HasHeader(response, "X-Amz-Cf-Pop"))
            {
                AddOnce(services, CloudFrontService);
            }
            if (HasHeader(response, "X-Azure-Ref")) AddOnce(services, AzureFrontDoorService);
            if (HasHeader(response, "X-Served-By") && HasHeader(response, "X-Timer"))
            {
                AddOnce(services, FastlyService);
            }
            if (HasHeader(response, "Fly-Request-Id")) AddOnce(services, FlyIoService);
            if (HasHeader(response, "X-Vercel-Id")) AddOnce(services, VercelService);

            string server = GetCombinedHeaderValue(response, "Server");
            AddServerHeaderService(services, server);
            return services;
        }

        /// <summary>
        /// Merges current-request services with a self-probe. Edge services are placed farthest from Cotton, while
        /// a concrete local service replaces an unidentified reverse-proxy hop.
        /// </summary>
        public static IReadOnlyList<string> MergeProxyServices(
            IReadOnlyList<string> requestServices,
            IReadOnlyList<string> probedServices)
        {
            ArgumentNullException.ThrowIfNull(requestServices);
            ArgumentNullException.ThrowIfNull(probedServices);

            List<string> merged = [.. requestServices];
            foreach (string service in probedServices.Where(EdgeProxyServiceNames.Contains).Reverse())
            {
                if (!merged.Contains(service, StringComparer.Ordinal)) merged.Insert(0, service);
            }

            foreach (string service in probedServices.Where(LocalProxyServiceNames.Contains))
            {
                if (merged.Contains(service, StringComparer.Ordinal)) continue;
                merged.Remove(GenericReverseProxyService);
                merged.Add(service);
            }

            return merged;
        }

        private static void AddDeclaredServices(HttpRequest request, List<string> services)
        {
            if (!request.Headers.TryGetValue(DeclaredProxyHeaderName, out var values))
            {
                return;
            }

            foreach (string? value in values)
            {
                foreach (string candidate in (value ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    if (DeclaredServiceNames.TryGetValue(candidate, out string? canonicalName))
                    {
                        AddOnce(services, canonicalName);
                    }
                }
            }
        }

        private static void AddOnce(List<string> services, string service)
        {
            if (!services.Contains(service, StringComparer.Ordinal))
            {
                services.Add(service);
            }
        }

        private static void AddServerHeaderService(List<string> services, string server)
        {
            if (string.IsNullOrWhiteSpace(server)) return;

            foreach ((string marker, string service) in ServerHeaderSignatures)
            {
                if (server.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    AddOnce(services, service);
                    return;
                }
            }
        }

        private static bool HasHeader(HttpResponseMessage response, string name)
        {
            return response.Headers.TryGetValues(name, out IEnumerable<string>? values)
                && values.Any(value => !string.IsNullOrWhiteSpace(value));
        }

        private static string GetCombinedHeaderValue(HttpResponseMessage response, string name)
        {
            return response.Headers.TryGetValues(name, out IEnumerable<string>? values)
                ? string.Join(' ', values)
                : string.Empty;
        }

        private static bool HasHeader(HttpRequest request, string name)
        {
            return request.Headers.TryGetValue(name, out var values)
                && values.Any(value => !string.IsNullOrWhiteSpace(value));
        }
    }
}
