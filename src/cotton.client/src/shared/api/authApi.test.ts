import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("react-toastify", () => ({
  toast: { error: vi.fn() },
}));

vi.mock("../i18n/translateError", () => ({
  translateError: (namespace: string, key: string) => `${namespace}:${key}`,
}));

const refreshEnabledMock = vi.fn<() => boolean>(() => true);
const logoutLocalMock = vi.fn<() => void>();

vi.mock("../store/authStore", () => ({
  getRefreshEnabled: () => refreshEnabledMock(),
  useAuthStore: {
    getState: () => ({
      hydrated: true,
      refreshEnabled: refreshEnabledMock(),
      logoutLocal: logoutLocalMock,
    }),
  },
}));

const {
  clearAccessToken,
  getAccessToken,
  httpClient,
  setAccessToken,
} = await import("./httpClient");
const { authApi } = await import("./authApi");

const baseUserResponse = {
  id: "user-1",
  createdAt: "2026-05-17T00:00:00Z",
  updatedAt: "2026-05-17T00:00:01Z",
  role: 2,
  username: "alice",
};

beforeEach(() => {
  refreshEnabledMock.mockReturnValue(true);
  logoutLocalMock.mockClear();
  vi.spyOn(console, "error").mockImplementation(() => undefined);
  clearAccessToken();
});

afterEach(() => {
  vi.restoreAllMocks();
  clearAccessToken();
});

describe("authApi.login", () => {
  it("posts credentials and stores the returned access token", async () => {
    const post = vi.spyOn(httpClient, "post").mockResolvedValue({
      data: { accessToken: "access-token" },
    });
    const credentials = {
      username: "alice",
      password: "secret",
      twoFactorCode: "123456",
      trustDevice: true,
    };

    const token = await authApi.login(credentials);

    expect(post).toHaveBeenCalledWith("auth/login", credentials);
    expect(token).toBe("access-token");
    expect(getAccessToken()).toBe("access-token");
  });
});

describe("authApi.me", () => {
  it("maps the full server response to the User shape", async () => {
    vi.spyOn(httpClient, "get").mockResolvedValue({
      data: {
        ...baseUserResponse,
        email: "alice@example.com",
        isEmailVerified: true,
        displayName: "Alice",
        pictureUrl: "https://cdn.example/avatar.png",
        avatarHashEncryptedHex: "hash value",
        preferences: { theme: "dark" },
        firstName: "Alice",
        lastName: "Belova",
        birthDate: "1990-01-01",
        isTotpEnabled: true,
        totpEnabledAt: "2026-05-17T00:10:00Z",
        totpFailedAttempts: 3,
      },
    });

    const user = await authApi.me();

    expect(httpClient.get).toHaveBeenCalledWith("auth/me");
    expect(user).toMatchObject({
      id: "user-1",
      role: 2,
      username: "alice",
      email: "alice@example.com",
      isEmailVerified: true,
      displayName: "Alice",
      pictureUrl: "/api/v1/preview/hash%20value.webp",
      avatarHashEncryptedHex: "hash value",
      preferences: { theme: "dark" },
      firstName: "Alice",
      lastName: "Belova",
      birthDate: "1990-01-01",
      createdAt: "2026-05-17T00:00:00Z",
      updatedAt: "2026-05-17T00:00:01Z",
      isTotpEnabled: true,
      totpEnabledAt: "2026-05-17T00:10:00Z",
      totpFailedAttempts: 3,
    });
  });

  it("fills stable defaults for optional profile fields", async () => {
    vi.spyOn(httpClient, "get").mockResolvedValue({
      data: baseUserResponse,
    });

    const user = await authApi.me();

    expect(user).toMatchObject({
      email: null,
      isEmailVerified: false,
      displayName: "alice",
      avatarHashEncryptedHex: null,
      firstName: null,
      lastName: null,
      birthDate: null,
      totpEnabledAt: null,
      totpFailedAttempts: 0,
    });
    expect(user.pictureUrl).toBeUndefined();
  });

  it("uses pictureUrl when no encrypted avatar hash is present", async () => {
    vi.spyOn(httpClient, "get").mockResolvedValue({
      data: {
        ...baseUserResponse,
        pictureUrl: "https://cdn.example/avatar.png",
        avatarHashEncryptedHex: "   ",
      },
    });

    await expect(authApi.me()).resolves.toMatchObject({
      pictureUrl: "https://cdn.example/avatar.png",
      avatarHashEncryptedHex: "   ",
    });
  });
});

describe("authApi profile mutations", () => {
  it("clears the access token before posting logout", async () => {
    setAccessToken("stale-token");
    const post = vi.spyOn(httpClient, "post").mockImplementation(() => {
      expect(getAccessToken()).toBeNull();
      return Promise.resolve({ data: undefined });
    });

    await authApi.logout();

    expect(post).toHaveBeenCalledWith("auth/logout");
  });

  it("updates the current profile and maps the returned user", async () => {
    const put = vi.spyOn(httpClient, "put").mockResolvedValue({
      data: {
        ...baseUserResponse,
        username: "alice2",
        displayName: "Alice 2",
      },
    });
    const request = {
      username: "alice2",
      email: "alice2@example.com",
      avatarHash: null,
    };

    const user = await authApi.updateProfile(request);

    expect(put).toHaveBeenCalledWith("users/me", request);
    expect(user).toMatchObject({
      username: "alice2",
      displayName: "Alice 2",
    });
  });

  it("changes the password through the current-user endpoint", async () => {
    const put = vi.spyOn(httpClient, "put").mockResolvedValue({
      data: undefined,
    });

    await authApi.changePassword({
      oldPassword: "old",
      newPassword: "new",
    });

    expect(put).toHaveBeenCalledWith("users/me/password", {
      oldPassword: "old",
      newPassword: "new",
    });
  });
});

describe("authApi account routes", () => {
  it("returns the WebDAV token response body", async () => {
    vi.spyOn(httpClient, "get").mockResolvedValue({
      data: "dav-token",
    });

    await expect(authApi.getWebDavToken()).resolves.toBe("dav-token");
    expect(httpClient.get).toHaveBeenCalledWith("auth/webdav/token");
  });

  it("posts forgot-password and reset-password payloads", async () => {
    const post = vi.spyOn(httpClient, "post").mockResolvedValue({
      data: undefined,
    });

    await authApi.forgotPassword("alice@example.com");
    await authApi.resetPassword("reset-token", "new-password");

    expect(post).toHaveBeenNthCalledWith(1, "auth/forgot-password", {
      usernameOrEmail: "alice@example.com",
    });
    expect(post).toHaveBeenNthCalledWith(2, "auth/reset-password", {
      token: "reset-token",
      newPassword: "new-password",
    });
  });

  it("sends and confirms email verification", async () => {
    const post = vi.spyOn(httpClient, "post").mockResolvedValue({
      data: undefined,
    });

    await authApi.sendEmailVerification();
    await authApi.confirmEmailVerification("a+b/c=d");

    expect(post).toHaveBeenNthCalledWith(1, "users/me/send-email-verification");
    expect(post).toHaveBeenNthCalledWith(
      2,
      "users/verify-email?token=a%2Bb%2Fc%3Dd",
    );
  });

  it("invalidates share links through the auth endpoint", async () => {
    const post = vi.spyOn(httpClient, "post").mockResolvedValue({
      data: undefined,
    });

    await authApi.invalidateShareLinks();

    expect(post).toHaveBeenCalledWith("auth/invalidate-share-links");
  });
});
