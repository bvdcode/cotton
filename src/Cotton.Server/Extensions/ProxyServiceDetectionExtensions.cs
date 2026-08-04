// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Dto;

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
        /// and must not be used to establish proxy trust.
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
        /// Returns normalized Cloudflare country and data-center hints from an incoming request.
        /// </summary>
        public static CloudflareProxyMetadataDto? DetectCloudflareMetadata(this HttpRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!HasHeader(request, "CF-Ray")) return null;

            return CreateCloudflareMetadata(
                GetFirstHeaderValue(request, "CF-IPCountry"),
                GetFirstHeaderValue(request, "CF-Ray"));
        }

        /// <summary>
        /// Returns normalized Cloudflare data-center hints from a probe response.
        /// </summary>
        public static CloudflareProxyMetadataDto? DetectCloudflareMetadata(HttpResponseMessage response)
        {
            ArgumentNullException.ThrowIfNull(response);
            if (!HasHeader(response, "CF-Ray")) return null;

            return CreateCloudflareMetadata(
                countryCode: null,
                rayId: GetFirstHeaderValue(response, "CF-Ray"));
        }

        /// <summary>
        /// Merges Cloudflare metadata, preferring the current request and filling only missing values from a probe.
        /// </summary>
        public static CloudflareProxyMetadataDto? MergeCloudflareMetadata(
            CloudflareProxyMetadataDto? requestMetadata,
            CloudflareProxyMetadataDto? probedMetadata)
        {
            if (requestMetadata is null)
            {
                return probedMetadata is null
                    ? null
                    : new()
                    {
                        VisitorCountryCode = null,
                        DatacenterCode = probedMetadata.DatacenterCode,
                    };
            }
            if (probedMetadata is null) return requestMetadata;

            return new()
            {
                VisitorCountryCode = requestMetadata.VisitorCountryCode ?? probedMetadata.VisitorCountryCode,
                DatacenterCode = requestMetadata.DatacenterCode ?? probedMetadata.DatacenterCode,
            };
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

        private static CloudflareProxyMetadataDto? CreateCloudflareMetadata(
            string? countryCode,
            string? rayId)
        {
            string? normalizedCountryCode = NormalizeCountryCode(countryCode);
            string? datacenterCode = ParseCloudflareDatacenterCode(rayId);
            return normalizedCountryCode is null && datacenterCode is null
                ? null
                : new()
                {
                    VisitorCountryCode = normalizedCountryCode,
                    DatacenterCode = datacenterCode,
                };
        }

        private static string? NormalizeCountryCode(string? value)
        {
            string normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
            if (normalized == "T1") return normalized;
            return normalized.Length == 2 && normalized.All(char.IsAsciiLetter)
                ? normalized
                : null;
        }

        private static string? ParseCloudflareDatacenterCode(string? rayId)
        {
            string value = (rayId ?? string.Empty).Split(',', 2)[0].Trim();
            int separatorIndex = value.LastIndexOf('-');
            if (separatorIndex < 0 || separatorIndex == value.Length - 1) return null;

            string code = value[(separatorIndex + 1)..].ToUpperInvariant();
            return code.Length == 3 && code.All(char.IsAsciiLetter) ? code : null;
        }

        private static string? GetFirstHeaderValue(HttpRequest request, string name)
        {
            return request.Headers.TryGetValue(name, out var values)
                ? values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                : null;
        }

        private static string? GetFirstHeaderValue(HttpResponseMessage response, string name)
        {
            return response.Headers.TryGetValues(name, out IEnumerable<string>? values)
                ? values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                : null;
        }

        private static bool HasHeader(HttpRequest request, string name)
        {
            return request.Headers.TryGetValue(name, out var values)
                && values.Any(value => !string.IsNullOrWhiteSpace(value));
        }
    }
}
