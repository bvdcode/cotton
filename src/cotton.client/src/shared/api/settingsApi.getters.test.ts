import { afterEach, describe, expect, it, vi } from "vitest";

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

describe("settingsApi getters", () => {
  it("validates public info and setup state responses", async () => {
    const get = vi
      .spyOn(httpClient, "get")
      .mockResolvedValueOnce({
        data: {
          product: "Cotton",
          instanceIdHash: "server-fingerprint",
          canCreateInitialAdmin: true,
          isPublicInstance: false,
        },
      })
      .mockResolvedValueOnce({
        data: { isServerInitialized: false },
      });

    await expect(settingsApi.getPublicInfo()).resolves.toEqual({
      product: "Cotton",
      instanceIdHash: "server-fingerprint",
      canCreateInitialAdmin: true,
      isPublicInstance: false,
    });
    await expect(settingsApi.getIsSetupComplete()).resolves.toBe(false);
    expect(get).toHaveBeenNthCalledWith(1, "server/info", undefined);
    expect(get).toHaveBeenNthCalledWith(
      2,
      "server/settings/is-setup-complete",
      undefined,
    );
  });

  it("normalizes the main settings response", async () => {
    vi.spyOn(httpClient, "get").mockResolvedValue({
      data: {
        version: " 1.2.3 ",
        maxChunkSizeBytes: 1024,
        supportedHashAlgorithms: ["", " SHA-256 "],
      },
    });

    await expect(settingsApi.get()).resolves.toEqual({
      version: "1.2.3",
      maxChunkSizeBytes: 1024,
      supportedHashAlgorithm: "SHA-256",
    });
  });

  it("accepts both chunk-size response shapes", async () => {
    const get = vi
      .spyOn(httpClient, "get")
      .mockResolvedValueOnce({ data: 4096 })
      .mockResolvedValueOnce({
        data: {
          maxChunkSizeBytes: 8192,
          supportedMaxChunkSizeBytes: [4096, 8192, 16384],
        },
      });

    await expect(settingsApi.getChunkSize()).resolves.toBe(4096);
    await expect(settingsApi.getChunkSizeSettings()).resolves.toEqual({
      maxChunkSizeBytes: 8192,
      supportedMaxChunkSizeBytes: [4096, 8192, 16384],
    });
    expect(get).toHaveBeenCalledWith("server/settings/chunk-size", undefined);
  });

  it("reads and updates storage pipeline settings", async () => {
    const payload = {
      compressionLevel: 1,
      minCompressionLevel: -10,
      maxCompressionLevel: 22,
      cipherChunkSizeBytes: 1048576,
      minCipherChunkSizeBytes: 8192,
      maxCipherChunkSizeBytes: 67108864,
      supportedCipherChunkSizeBytes: [131072, 1048576],
      encryptionThreads: 2,
      minEncryptionThreads: 1,
      maxEncryptionThreads: 4,
      supportedEncryptionThreads: [1, 2, 3, 4],
    };
    const get = vi
      .spyOn(httpClient, "get")
      .mockResolvedValue({ data: payload });
    const patch = vi.spyOn(httpClient, "patch").mockResolvedValue({
      data: { ...payload, compressionLevel: -5 },
    });

    await expect(settingsApi.getStoragePipelineSettings()).resolves.toEqual(
      payload,
    );
    await expect(settingsApi.setCompressionLevel(-5)).resolves.toEqual({
      ...payload,
      compressionLevel: -5,
    });

    expect(get).toHaveBeenCalledWith(
      "server/settings/storage-pipeline",
      undefined,
    );
    expect(patch).toHaveBeenCalledWith("server/settings/compression-level/-5");
  });

  it("unwraps booleans and simple string settings", async () => {
    const get = vi
      .spyOn(httpClient, "get")
      .mockResolvedValueOnce({ data: { telemetryEnabled: true } })
      .mockResolvedValueOnce({ data: { disableVersionCheck: true } })
      .mockResolvedValueOnce({
        data: { allowCrossUserDeduplication: false },
      })
      .mockResolvedValueOnce({ data: { allowGlobalIndexing: true } })
      .mockResolvedValueOnce({ data: { timezone: null } })
      .mockResolvedValueOnce({ data: { publicBaseUrl: null } })
      .mockResolvedValueOnce({
        data: { defaultUserStorageQuotaBytes: 1073741824 },
      })
      .mockResolvedValueOnce({
        data: {
          defaultUserTemplateNodeId: "019e2537-492b-77d3-83e4-efe942c6156c",
        },
      });

    await expect(settingsApi.getTelemetry()).resolves.toBe(true);
    await expect(settingsApi.getDisableVersionCheck()).resolves.toBe(true);
    await expect(settingsApi.getAllowCrossUserDeduplication()).resolves.toBe(
      false,
    );
    await expect(settingsApi.getAllowGlobalIndexing()).resolves.toBe(true);
    await expect(settingsApi.getTimezone()).resolves.toBe("UTC");
    await expect(settingsApi.getPublicBaseUrl()).resolves.toBe("");
    await expect(settingsApi.getDefaultUserStorageQuotaBytes()).resolves.toBe(
      1073741824,
    );
    await expect(settingsApi.getDefaultUserTemplateNodeId()).resolves.toBe(
      "019e2537-492b-77d3-83e4-efe942c6156c",
    );
    expect(get).toHaveBeenNthCalledWith(
      1,
      "server/settings/telemetry",
      undefined,
    );
    expect(get).toHaveBeenNthCalledWith(
      2,
      "server/settings/disable-version-check",
      undefined,
    );
    expect(get).toHaveBeenNthCalledWith(
      3,
      "server/settings/allow-cross-user-deduplication",
      undefined,
    );
    expect(get).toHaveBeenNthCalledWith(
      4,
      "server/settings/allow-global-indexing",
      undefined,
    );
    expect(get).toHaveBeenNthCalledWith(
      5,
      "server/settings/timezone",
      undefined,
    );
    expect(get).toHaveBeenNthCalledWith(
      6,
      "server/settings/public-base-url",
      undefined,
    );
    expect(get).toHaveBeenNthCalledWith(
      7,
      "server/settings/default-user-storage-quota-bytes",
      undefined,
    );
    expect(get).toHaveBeenNthCalledWith(
      8,
      "server/settings/default-user-template-node",
      undefined,
    );
  });

  it("normalizes enum and usage responses", async () => {
    vi.spyOn(httpClient, "get")
      .mockResolvedValueOnce({ data: { serverUsage: ["photos", 3, "bad"] } })
      .mockResolvedValueOnce({ data: { storageSpaceMode: "limited" } })
      .mockResolvedValueOnce({ data: { computionMode: "remote" } })
      .mockResolvedValueOnce({ data: { storageType: "s3" } })
      .mockResolvedValueOnce({ data: { emailMode: "custom" } })
      .mockResolvedValueOnce({ data: { geoIpLookupMode: "maxmindlocal" } });

    await expect(settingsApi.getServerUsage()).resolves.toEqual([
      "Photos",
      "Media",
      "Other",
    ]);
    await expect(settingsApi.getStorageSpaceMode()).resolves.toBe("Limited");
    await expect(settingsApi.getComputionMode()).resolves.toBe("Remote");
    await expect(settingsApi.getStorageType()).resolves.toBe("S3");
    await expect(settingsApi.getEmailMode()).resolves.toBe("Custom");
    await expect(settingsApi.getGeoIpLookupMode()).resolves.toBe(
      "MaxMindLocal",
    );
  });

  it("normalizes config responses", async () => {
    vi.spyOn(httpClient, "get")
      .mockResolvedValueOnce({
        data: {
          endpoint: "https://s3.example",
          region: null,
          bucket: "cotton",
          accessKey: null,
          secretKey: "secret",
        },
      })
      .mockResolvedValueOnce({
        data: {
          smtpServer: "smtp.example",
          port: null,
          username: "mailer",
          password: null,
          fromAddress: "noreply@example.com",
        },
      })
      .mockResolvedValueOnce({
        data: { customGeoIpLookupUrl: null },
      })
      .mockResolvedValueOnce({
        data: { remoteComputationRunnerUrl: "https://runner.example" },
      });

    await expect(settingsApi.getS3Config()).resolves.toEqual({
      endpoint: "https://s3.example",
      region: "",
      bucket: "cotton",
      accessKey: "",
      secretKey: "secret",
    });
    await expect(settingsApi.getEmailConfig()).resolves.toEqual({
      smtpServer: "smtp.example",
      port: "",
      username: "mailer",
      password: "",
      fromAddress: "noreply@example.com",
      useSSL: false,
    });
    await expect(settingsApi.getCustomGeoIpLookupUrl()).resolves.toBe("");
    await expect(settingsApi.getRemoteComputationRunnerUrl()).resolves.toBe(
      "https://runner.example",
    );
  });
});
