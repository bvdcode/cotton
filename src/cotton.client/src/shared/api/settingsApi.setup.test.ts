import { afterEach, describe, expect, it, vi } from "vitest";
import { getRecentClientDiagnostics } from "../utils/clientDiagnostics";
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

    expect(patch).toHaveBeenNthCalledWith(1, "server/settings/server-usage", [
      "Photos",
      "Documents",
      "Media",
      "Other",
    ]);
    expect(patch).toHaveBeenNthCalledWith(2, "server/settings/server-usage", [
      "Other",
    ]);
  });

  it("defers external modes until their config steps", async () => {
    const patch = vi.spyOn(httpClient, "patch").mockResolvedValue({
      data: undefined,
    });

    await settingsApi.saveSetupStep("storage", { storage: "S3" });
    await settingsApi.saveSetupStep("email", { email: "custom" });
    await settingsApi.saveSetupStep("computionMode", {
      computionMode: "remote",
    });

    expect(patch).not.toHaveBeenCalled();
  });

  it("saves local/simple setup choices immediately", async () => {
    const patch = vi.spyOn(httpClient, "patch").mockResolvedValue({
      data: undefined,
      status: 204,
    });

    await settingsApi.saveSetupStep("telemetry", { telemetry: true });
    await settingsApi.saveSetupStep("storage", { storage: "local" });
    await settingsApi.saveSetupStep("geoIpLookupMode", {
      geoIpLookupMode: "cottoncloud",
    });
    await settingsApi.saveSetupStep("computionMode", {
      computionMode: "local",
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
      "server/settings/compution-mode/Local",
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
      data: readyComputationStatus,
    });
    const post = vi
      .spyOn(httpClient, "post")
      .mockResolvedValueOnce({
        data: undefined,
      })
      .mockResolvedValueOnce({
        data: {
          inputLabel: "Google DNS IP",
          inputValue: "8.8.8.8",
          country: "United States",
          region: null,
          city: null,
        },
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
    await settingsApi.saveSetupStep("remoteComputationRunnerUrl", {
      remoteComputationRunnerUrl: { url: " https://runner.example " },
    });

    expect(patch).toHaveBeenNthCalledWith(1, "server/settings/s3-config", {
      endpoint: "https://s3.example",
      region: "eu",
      bucket: "cotton",
      accessKey: "key",
      secretKey: "secret",
    });
    expect(patch).toHaveBeenNthCalledWith(2, "server/settings/storage-type/S3");
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
    expect(patch).toHaveBeenNthCalledWith(
      7,
      "server/settings/remote-computation-runner-url",
      "https://runner.example",
    );
    expect(patch).toHaveBeenCalledTimes(7);
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
    const saveStep = vi
      .spyOn(settingsApi, "saveSetupStep")
      .mockRejectedValueOnce(new Error("failed"))
      .mockResolvedValue(undefined);
    await settingsApi.saveSetupAnswers({
      trustedMode: "family",
      telemetry: true,
    });

    const warning = getRecentClientDiagnostics().at(-1);
    expect(warning?.source).toBe("app.warning");
    expect(warning?.message).toContain('Failed to save setup step "trustedMode"');
    expect(saveStep).toHaveBeenCalledWith("telemetry", {
      trustedMode: "family",
      telemetry: true,
    });
  });
});
