import { afterEach, describe, expect, it, vi } from "vitest";
import { readyComputationStatus } from "../../test/computationStatus";

vi.mock("@shared/ui/notifications", () => ({
  toast: { error: vi.fn() },
}));

vi.mock("../i18n/translateError", () => ({
  translateError: (namespace: string, key: string) => `${namespace}:${key}`,
}));

vi.mock("../store/authStore", () => ({
  getRefreshEnabled: () => true,
  useAuthStore: {
    getState: () => ({
      logoutLocal: vi.fn(),
    }),
  },
}));

const { httpClient } = await import("./httpClient");
const { settingsApi } = await import("./settingsApi");

afterEach(() => {
  vi.restoreAllMocks();
});

describe("settingsApi setters", () => {
  it("detects and verifies the trusted proxy address", async () => {
    const get = vi
      .spyOn(httpClient, "get")
      .mockResolvedValueOnce({
        data: { trustedProxyIpAddress: "172.18.0.2" },
      })
      .mockResolvedValueOnce({
        data: {
          observedProxyIpAddress: "172.18.0.3",
          suggestedTrustedProxy: "172.16.0.0/12",
          detectedProxyServices: ["cloudflare", "reverse-proxy"],
          cloudflare: {
            visitorCountryCode: "US",
            datacenterCode: "SJC",
          },
        },
        headers: { server: "nginx/1.27.4", "cf-ray": "a2591eb86ff8cbaa-LAX" },
      });
    const post = vi.spyOn(httpClient, "post").mockResolvedValue({
      data: {
        trustedProxyIpAddress: "172.18.0.3",
        observedProxyIpAddress: "172.18.0.3",
        detectedProxyServices: ["cloudflare", "reverse-proxy"],
        cloudflare: null,
        matches: true,
        saved: true,
      },
      headers: { server: "cloudflare" },
    });

    await expect(settingsApi.getTrustedProxyIpAddress()).resolves.toBe(
      "172.18.0.2",
    );
    await expect(settingsApi.getObservedProxyInfo()).resolves.toEqual({
      observedProxyIpAddress: "172.18.0.3",
      suggestedTrustedProxy: "172.16.0.0/12",
      detectedProxyServices: ["cloudflare", "nginx"],
      cloudflare: {
        visitorCountryCode: "US",
        datacenterCode: "LAX",
      },
    });
    await expect(
      settingsApi.verifyAndSaveTrustedProxyIpAddress("172.18.0.3"),
    ).resolves.toEqual({
      trustedProxyIpAddress: "172.18.0.3",
      observedProxyIpAddress: "172.18.0.3",
      detectedProxyServices: ["cloudflare", "reverse-proxy"],
      cloudflare: null,
      matches: true,
      saved: true,
    });

    expect(get).toHaveBeenNthCalledWith(
      1,
      "server/settings/trusted-proxy-ip-address",
      undefined,
    );
    expect(get).toHaveBeenNthCalledWith(
      2,
      "server/settings/trusted-proxy-ip-address/observed",
    );
    expect(post).toHaveBeenCalledWith(
      "server/settings/trusted-proxy-ip-address/verify-and-save",
      "172.18.0.3",
    );
  });

  it("patches primitive settings with the expected payloads", async () => {
    const patch = vi.spyOn(httpClient, "patch").mockResolvedValue({
      data: undefined,
    });

    await settingsApi.setTelemetry(true);
    await settingsApi.setDisableVersionCheck(true);
    await settingsApi.setAllowCrossUserDeduplication(false);
    await settingsApi.setAllowGlobalIndexing(true);
    await settingsApi.setServerUsage(["Photos", "Documents"]);
    await settingsApi.setTimezone("Europe/Amsterdam");
    await settingsApi.setPublicBaseUrl("https://cotton.example");
    await settingsApi.setDefaultUserStorageQuotaBytes(1073741824);
    await settingsApi.setDefaultUserTemplateNodeId(
      "019e2537-492b-77d3-83e4-efe942c6156c",
    );

    expect(patch).toHaveBeenNthCalledWith(1, "server/settings/telemetry", true);
    expect(patch).toHaveBeenNthCalledWith(
      2,
      "server/settings/disable-version-check",
      true,
    );
    expect(patch).toHaveBeenNthCalledWith(
      3,
      "server/settings/allow-cross-user-deduplication",
      false,
    );
    expect(patch).toHaveBeenNthCalledWith(
      4,
      "server/settings/allow-global-indexing",
      true,
    );
    expect(patch).toHaveBeenNthCalledWith(5, "server/settings/server-usage", [
      "Photos",
      "Documents",
    ]);
    expect(patch).toHaveBeenNthCalledWith(
      6,
      "server/settings/timezone",
      "Europe/Amsterdam",
    );
    expect(patch).toHaveBeenNthCalledWith(
      7,
      "server/settings/public-base-url",
      "https://cotton.example",
    );
    expect(patch).toHaveBeenNthCalledWith(
      8,
      "server/settings/default-user-storage-quota-bytes",
      1073741824,
    );
    expect(patch).toHaveBeenNthCalledWith(
      9,
      "server/settings/default-user-template-node",
      "019e2537-492b-77d3-83e4-efe942c6156c",
    );
  });

  it("encodes mode setters in the URL path", async () => {
    const patch = vi
      .spyOn(httpClient, "patch")
      .mockResolvedValue({ data: undefined })
      .mockResolvedValueOnce({ data: undefined })
      .mockResolvedValueOnce({
        data: {
          maxChunkSizeBytes: 16777216,
          supportedMaxChunkSizeBytes: [4194304, 8388608, 16777216],
        },
      })
      .mockResolvedValueOnce({ data: undefined, status: 204 });

    await settingsApi.setStorageSpaceMode("Limited");
    await settingsApi.setChunkSize(16777216);
    await settingsApi.setComputionMode("Remote");
    await settingsApi.setStorageType("S3");
    await settingsApi.setEmailMode("Custom");
    await settingsApi.setGeoIpLookupMode("CottonCloud");

    expect(patch).toHaveBeenNthCalledWith(
      1,
      "server/settings/storage-space-mode/Limited",
    );
    expect(patch).toHaveBeenNthCalledWith(
      2,
      "server/settings/chunk-size/16777216",
    );
    expect(patch).toHaveBeenNthCalledWith(
      3,
      "server/settings/compution-mode/Remote",
    );
    expect(patch).toHaveBeenNthCalledWith(4, "server/settings/storage-type/S3");
    expect(patch).toHaveBeenNthCalledWith(
      5,
      "server/settings/email-mode/Custom",
    );
    expect(patch).toHaveBeenNthCalledWith(
      6,
      "server/settings/geoip-lookup-mode/CottonCloud",
    );
  });

  it("returns the validated computation status when cloud mode is enabled", async () => {
    vi.spyOn(httpClient, "patch").mockResolvedValue({
      data: readyComputationStatus,
      status: 200,
    });

    await expect(settingsApi.setComputionMode("Cloud")).resolves.toEqual(
      readyComputationStatus,
    );
  });

  it("patches object configs and calls test endpoints", async () => {
    const patch = vi.spyOn(httpClient, "patch").mockResolvedValue({
      data: readyComputationStatus,
    });
    const geoIpTestResult = {
      inputLabel: "Google DNS IP",
      inputValue: "8.8.8.8",
      country: "United States",
      region: null,
      city: null,
    };
    const post = vi
      .spyOn(httpClient, "post")
      .mockResolvedValueOnce({
        data: undefined,
      })
      .mockResolvedValueOnce({
        data: geoIpTestResult,
      });
    const s3Config = {
      endpoint: "https://s3.example",
      region: "eu",
      bucket: "cotton",
      accessKey: "key",
      secretKey: "secret",
    };
    const emailConfig = {
      smtpServer: "smtp.example",
      port: "587",
      username: "mailer",
      password: "secret",
      fromAddress: "noreply@example.com",
      useSSL: true,
    };

    await settingsApi.setS3Config(s3Config);
    await settingsApi.setEmailConfig(emailConfig);
    await settingsApi.testEmailConfig();
    await settingsApi.setCustomGeoIpLookupUrl("https://geo.example");
    await settingsApi.setRemoteComputationRunnerUrl("https://runner.example");
    await expect(settingsApi.testCustomGeoIpLookupUrl()).resolves.toEqual(
      geoIpTestResult,
    );

    expect(patch).toHaveBeenNthCalledWith(
      1,
      "server/settings/s3-config",
      s3Config,
    );
    expect(patch).toHaveBeenNthCalledWith(
      2,
      "server/settings/email-config",
      emailConfig,
    );
    expect(post).toHaveBeenNthCalledWith(
      1,
      "server/settings/email-config/test",
    );
    expect(patch).toHaveBeenNthCalledWith(
      3,
      "server/settings/custom-geoip-lookup-url",
      "https://geo.example",
    );
    expect(patch).toHaveBeenNthCalledWith(
      4,
      "server/settings/remote-computation-runner-url",
      "https://runner.example",
    );
    expect(post).toHaveBeenNthCalledWith(
      2,
      "server/settings/custom-geoip-lookup-url/test",
    );
  });
});
