import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

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
const { isSelfPreferenceUpdateToken, userPreferencesApi } = await import(
  "./userPreferencesApi"
);

beforeEach(() => {
  vi.spyOn(console, "error").mockImplementation(() => undefined);
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("userPreferencesApi.update", () => {
  it("sends a self-update token by default", async () => {
    const patch = { themeMode: "dark" };
    const httpPatch = vi.spyOn(httpClient, "patch").mockResolvedValue({
      data: patch,
    });

    await expect(userPreferencesApi.update(patch)).resolves.toEqual(patch);

    const config = httpPatch.mock.calls[0]?.[2];
    const token = config?.params?.token;
    expect(httpPatch).toHaveBeenCalledWith("users/me/preferences", patch, {
      params: { token },
    });
    expect(typeof token).toBe("string");
    expect(token.length).toBeGreaterThan(0);
    expect(isSelfPreferenceUpdateToken(token)).toBe(true);
  });

  it("allows callers to pass an explicit update token", async () => {
    const patch = { uiLanguage: "ru" };
    const httpPatch = vi.spyOn(httpClient, "patch").mockResolvedValue({
      data: patch,
    });

    await userPreferencesApi.update(patch, { token: "external-tab" });

    expect(httpPatch).toHaveBeenCalledWith("users/me/preferences", patch, {
      params: { token: "external-tab" },
    });
  });
});
