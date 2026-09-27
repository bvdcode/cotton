import type {
  CloudflareProxyMetadata,
  DetectedProxyService,
} from "./schemas/serverSettings";

const responseServerSignatures: ReadonlyArray<
  readonly [DetectedProxyService, RegExp]
> = [
  ["cloudflare", /\bcloudflare\b/i],
  ["cloudfront", /\bcloudfront\b/i],
  ["fastly", /\bfastly\b/i],
  ["fly-io", /\bfly(?:\.io)?\b/i],
  ["vercel", /\bvercel\b/i],
  ["aws-alb", /\bawselb\b/i],
  ["traefik", /\btraefik\b/i],
  ["envoy", /\benvoy\b/i],
  ["nginx", /\bnginx\b/i],
  ["caddy", /\bcaddy\b/i],
  ["haproxy", /\bhaproxy\b/i],
  ["apache", /\bapache\b/i],
];

const localProxyServices = new Set<DetectedProxyService>([
  "traefik",
  "envoy",
  "nginx",
  "caddy",
  "haproxy",
  "apache",
]);

const edgeProxyServices = new Set<DetectedProxyService>([
  "cloudflare",
  "cloudfront",
  "azure-front-door",
  "fastly",
  "fly-io",
  "vercel",
  "aws-alb",
]);

const parseCloudflareDatacenterCode = (
  value: AxiosHeaderValue | undefined,
): string | null => {
  if (typeof value !== "string") return null;
  const firstValue = value.split(",", 1)[0].trim();
  const separatorIndex = firstValue.lastIndexOf("-");
  if (separatorIndex < 0) return null;
  const code = firstValue.slice(separatorIndex + 1).toUpperCase();
  return /^[A-Z]{3}$/.test(code) ? code : null;
};

export const mergeCloudflareMetadata = (
  metadata: CloudflareProxyMetadata,
  rayHeader: AxiosHeaderValue | undefined,
): CloudflareProxyMetadata => {
  const browserDatacenterCode = parseCloudflareDatacenterCode(rayHeader);
  if (!browserDatacenterCode) return metadata;
  return {
    visitorCountryCode: metadata?.visitorCountryCode ?? null,
    datacenterCode: browserDatacenterCode,
  };
};

const detectResponseServerService = (
  serverHeader: AxiosHeaderValue | undefined,
): DetectedProxyService[] => {
  if (typeof serverHeader !== "string") return [];
  const match = responseServerSignatures.find(([, pattern]) =>
    pattern.test(serverHeader),
  );
  return match ? [match[0]] : [];
};

export const mergeDetectedProxyServices = (
  detected: DetectedProxyService[],
  serverHeader: AxiosHeaderValue | undefined,
): DetectedProxyService[] => {
  const responseServices = detectResponseServerService(serverHeader);
  const identifiesUnknownLocalProxy = responseServices.some(
    (service) => localProxyServices.has(service) && !detected.includes(service),
  );
  const merged = identifiesUnknownLocalProxy
    ? detected.filter((service) => service !== "reverse-proxy")
    : [...detected];
  for (const service of responseServices) {
    if (merged.includes(service)) continue;
    if (edgeProxyServices.has(service)) merged.unshift(service);
    else merged.push(service);
  }
  return merged;
};
import type { AxiosHeaderValue } from "axios";
