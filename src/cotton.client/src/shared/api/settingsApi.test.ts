import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("react-toastify", () => ({
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

beforeEach(() => {
  vi.spyOn(console, "error").mockImplementation(() => undefined);
  vi.spyOn(console, "warn").mockImplementation(() => undefined);
});

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
          instanceIdHash: "hash",
          canCreateInitialAdmin: true,
        },
      })
      .mockResolvedValueOnce({
        data: { isServerInitialized: false },
      });

    await expect(settingsApi.getPublicInfo()).resolves.toEqual({
      product: "Cotton",
      instanceIdHash: "hash",
      canCreateInitialAdmin: true,
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
      .mockResolvedValueOnce({ data: { maxChunkSizeBytes: 8192 } });

    await expect(settingsApi.getChunkSize()).resolves.toBe(4096);
    await expect(settingsApi.getChunkSize()).resolves.toBe(8192);
    expect(get).toHaveBeenCalledWith(
      "server/settings/chunk-size",
      undefined,
    );
  });

  it("unwraps booleans and simple string settings", async () => {
    const get = vi
      .spyOn(httpClient, "get")
      .mockResolvedValueOnce({ data: { telemetryEnabled: true } })
      .mockResolvedValueOnce({
        data: { allowCrossUserDeduplication: false },
      })
      .mockResolvedValueOnce({ data: { allowGlobalIndexing: true } })
      .mockResolvedValueOnce({ data: { timezone: null } })
      .mockResolvedValueOnce({ data: { publicBaseUrl: null } });

    await expect(settingsApi.getTelemetry()).resolves.toBe(true);
    await expect(settingsApi.getAllowCrossUserDeduplication()).resolves.toBe(
      false,
    );
    await expect(settingsApi.getAllowGlobalIndexing()).resolves.toBe(true);
    await expect(settingsApi.getTimezone()).resolves.toBe("UTC");
    await expect(settingsApi.getPublicBaseUrl()).resolves.toBe("");
    expect(get).toHaveBeenNthCalledWith(
      1,
      "server/settings/telemetry",
      undefined,
    );
    expect(get).toHaveBeenNthCalledWith(
      2,
      "server/settings/allow-cross-user-deduplication",
      undefined,
    );
    expect(get).toHaveBeenNthCalledWith(
      3,
      "server/settings/allow-global-indexing",
      undefined,
    );
    expect(get).toHaveBeenNthCalledWith(
      4,
      "server/settings/timezone",
      undefined,
    );
    expect(get).toHaveBeenNthCalledWith(
      5,
      "server/settings/public-base-url",
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
  });
});

describe("settingsApi setters", () => {
  it("patches primitive settings with the expected payloads", async () => {
    const patch = vi.spyOn(httpClient, "patch").mockResolvedValue({
      data: undefined,
    });

    await settingsApi.setTelemetry(true);
    await settingsApi.setAllowCrossUserDeduplication(false);
    await settingsApi.setAllowGlobalIndexing(true);
    await settingsApi.setServerUsage(["Photos", "Documents"]);
    await settingsApi.setTimezone("Europe/Amsterdam");
    await settingsApi.setPublicBaseUrl("https://cotton.example");

    expect(patch).toHaveBeenNthCalledWith(1, "server/settings/telemetry", true);
    expect(patch).toHaveBeenNthCalledWith(
      2,
      "server/settings/allow-cross-user-deduplication",
      false,
    );
    expect(patch).toHaveBeenNthCalledWith(
      3,
      "server/settings/allow-global-indexing",
      true,
    );
    expect(patch).toHaveBeenNthCalledWith(
      4,
      "server/settings/server-usage",
      ["Photos", "Documents"],
    );
    expect(patch).toHaveBeenNthCalledWith(
      5,
      "server/settings/timezone",
      "Europe/Amsterdam",
    );
    expect(patch).toHaveBeenNthCalledWith(
      6,
      "server/settings/public-base-url",
      "https://cotton.example",
    );
  });

  it("encodes mode setters in the URL path", async () => {
    const patch = vi.spyOn(httpClient, "patch").mockResolvedValue({
      data: undefined,
    });

    await settingsApi.setStorageSpaceMode("Limited");
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
      "server/settings/compution-mode/Remote",
    );
    expect(patch).toHaveBeenNthCalledWith(
      3,
      "server/settings/storage-type/S3",
    );
    expect(patch).toHaveBeenNthCalledWith(
      4,
      "server/settings/email-mode/Custom",
    );
    expect(patch).toHaveBeenNthCalledWith(
      5,
      "server/settings/geoip-lookup-mode/CottonCloud",
    );
  });

  it("patches object configs and calls test endpoints", async () => {
    const patch = vi.spyOn(httpClient, "patch").mockResolvedValue({
      data: undefined,
    });
    const post = vi.spyOn(httpClient, "post").mockResolvedValue({
      data: undefined,
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
    await settingsApi.testCustomGeoIpLookupUrl();

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
    expect(post).toHaveBeenNthCalledWith(
      2,
      "server/settings/custom-geoip-lookup-url/test",
    );
  });
});

describe("settingsApi.saveSetupStep", () => {
  it("maps trusted mode answers into the two trust flags", async () => {
    const patch = vi.spyOn(httpClient, "patch").mockResolvedValue({
      data: undefined,
    });

    await settingsApi.saveSetupStep("trustedMode", { trustedMode: "family" });
    await settingsApi.saveSetupStep("trustedMode", { trustedMode: "unknown" });
    await settingsApi.saveSetupStep("trustedMode", { trustedMode: "private" });

    expect(patch).toHaveBeenNthCalledWith(
      1,
      "server/settings/allow-cross-user-deduplication",
      true,
    );
    expect(patch).toHaveBeenNthCalledWith(
      2,
      "server/settings/allow-global-indexing",
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
    expect(patch).toHaveBeenNthCalledWith(
      5,
      "server/settings/allow-cross-user-deduplication",
      false,
    );
    expect(patch).toHaveBeenNthCalledWith(
      6,
      "server/settings/allow-global-indexing",
      false,
    );
  });

  it("does not fail trusted mode when at least one flag save succeeds", async () => {
    vi.spyOn(httpClient, "patch")
      .mockRejectedValueOnce(new Error("first failed"))
      .mockResolvedValueOnce({ data: undefined });

    await expect(
      settingsApi.saveSetupStep("trustedMode", { trustedMode: "family" }),
    ).resolves.toBeUndefined();
  });

  it("throws trusted mode when every flag save fails", async () => {
    vi.spyOn(httpClient, "patch").mockRejectedValue(new Error("failed"));

    await expect(
      settingsApi.saveSetupStep("trustedMode", { trustedMode: "family" }),
    ).rejects.toThrow("failed");
  });

  it("maps usage answers to canonical values with a safe fallback", async () => {
    const patch = vi.spyOn(httpClient, "patch").mockResolvedValue({
      data: undefined,
    });

    await settingsApi.saveSetupStep("usage", {
      usage: ["photos", "DOCUMENTS", "media", "unknown", false],
    });
    await settingsApi.saveSetupStep("usage", { usage: [] });

    expect(patch).toHaveBeenNthCalledWith(
      1,
      "server/settings/server-usage",
      ["Photos", "Documents", "Media", "Other"],
    );
    expect(patch).toHaveBeenNthCalledWith(
      2,
      "server/settings/server-usage",
      ["Other"],
    );
  });

  it("defers external storage and email modes until config steps", async () => {
    const patch = vi.spyOn(httpClient, "patch").mockResolvedValue({
      data: undefined,
    });

    await settingsApi.saveSetupStep("storage", { storage: "S3" });
    await settingsApi.saveSetupStep("email", { email: "custom" });

    expect(patch).not.toHaveBeenCalled();
  });

  it("saves local/simple setup choices immediately", async () => {
    const patch = vi.spyOn(httpClient, "patch").mockResolvedValue({
      data: undefined,
    });

    await settingsApi.saveSetupStep("telemetry", { telemetry: true });
    await settingsApi.saveSetupStep("storage", { storage: "local" });
    await settingsApi.saveSetupStep("geoIpLookupMode", {
      geoIpLookupMode: "cottoncloud",
    });
    await settingsApi.saveSetupStep("computionMode", {
      computionMode: "remote",
    });
    await settingsApi.saveSetupStep("timezone", {
      timezone: "Europe/Amsterdam",
    });
    await settingsApi.saveSetupStep("storageSpace", {
      storageSpace: "unlimited",
    });

    expect(patch).toHaveBeenNthCalledWith(1, "server/settings/telemetry", true);
    expect(patch).toHaveBeenNthCalledWith(
      2,
      "server/settings/storage-type/Local",
    );
    expect(patch).toHaveBeenNthCalledWith(
      3,
      "server/settings/geoip-lookup-mode/CottonCloud",
    );
    expect(patch).toHaveBeenNthCalledWith(
      4,
      "server/settings/compution-mode/Remote",
    );
    expect(patch).toHaveBeenNthCalledWith(
      5,
      "server/settings/timezone",
      "Europe/Amsterdam",
    );
    expect(patch).toHaveBeenNthCalledWith(
      6,
      "server/settings/storage-space-mode/Unlimited",
    );
  });

  it("saves config steps, enables their modes, and runs validation calls", async () => {
    const patch = vi.spyOn(httpClient, "patch").mockResolvedValue({
      data: undefined,
    });
    const post = vi.spyOn(httpClient, "post").mockResolvedValue({
      data: undefined,
    });

    await settingsApi.saveSetupStep("s3Config", {
      s3Config: {
        endpoint: "https://s3.example",
        region: "eu",
        bucket: "cotton",
        accessKey: "key",
        secretKey: "secret",
      },
    });
    await settingsApi.saveSetupStep("emailConfig", {
      emailConfig: {
        smtpServer: "smtp.example",
        port: "587",
        username: "mailer",
        password: "secret",
        fromAddress: "noreply@example.com",
        useSSL: true,
      },
    });
    await settingsApi.saveSetupStep("customGeoIpLookupUrl", {
      customGeoIpLookupUrl: { url: "https://geo.example" },
    });

    expect(patch).toHaveBeenNthCalledWith(1, "server/settings/s3-config", {
      endpoint: "https://s3.example",
      region: "eu",
      bucket: "cotton",
      accessKey: "key",
      secretKey: "secret",
    });
    expect(patch).toHaveBeenNthCalledWith(
      2,
      "server/settings/storage-type/S3",
    );
    expect(patch).toHaveBeenNthCalledWith(3, "server/settings/email-config", {
      smtpServer: "smtp.example",
      port: "587",
      username: "mailer",
      password: "secret",
      fromAddress: "noreply@example.com",
      useSSL: true,
    });
    expect(patch).toHaveBeenNthCalledWith(
      4,
      "server/settings/email-mode/Custom",
    );
    expect(post).toHaveBeenNthCalledWith(
      1,
      "server/settings/email-config/test",
    );
    expect(patch).toHaveBeenNthCalledWith(
      5,
      "server/settings/custom-geoip-lookup-url",
      "https://geo.example",
    );
    expect(patch).toHaveBeenNthCalledWith(
      6,
      "server/settings/geoip-lookup-mode/CustomHttp",
    );
    expect(post).toHaveBeenNthCalledWith(
      2,
      "server/settings/custom-geoip-lookup-url/test",
    );
  });

  it("ignores unknown steps and empty timezone answers", async () => {
    const patch = vi.spyOn(httpClient, "patch").mockResolvedValue({
      data: undefined,
    });

    await settingsApi.saveSetupStep("timezone", { timezone: "" });
    await settingsApi.saveSetupStep("doesNotExist", { foo: "bar" });

    expect(patch).not.toHaveBeenCalled();
  });
});

describe("settingsApi.saveSetupAnswers", () => {
  it("continues after a failed step and warns once", async () => {
    const patch = vi
      .spyOn(httpClient, "patch")
      .mockRejectedValueOnce(new Error("failed"))
      .mockRejectedValueOnce(new Error("failed"))
      .mockResolvedValue({ data: undefined });
    const warn = vi.spyOn(console, "warn");

    await settingsApi.saveSetupAnswers({
      trustedMode: "family",
      telemetry: true,
    });

    expect(warn).toHaveBeenCalledWith(
      'Failed to save setup step "trustedMode"',
      expect.any(Error),
    );
    expect(patch).toHaveBeenCalledWith("server/settings/telemetry", true);
  });
});
