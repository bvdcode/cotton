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
const { storageQuotaApi } = await import("./storageQuotaApi");

afterEach(() => {
  vi.restoreAllMocks();
});

describe("storageQuotaApi", () => {
  it("loads the current user's quota snapshot", async () => {
    const response = {
      usedBytes: 1024,
      quotaBytes: 2048,
      availableBytes: 1024,
    };
    const get = vi.spyOn(httpClient, "get").mockResolvedValue({
      data: response,
    });

    await expect(storageQuotaApi.getCurrent()).resolves.toEqual(response);

    expect(get).toHaveBeenCalledWith("users/me/storage-quota");
  });
});
