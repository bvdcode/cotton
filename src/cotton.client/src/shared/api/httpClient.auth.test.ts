import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  AxiosError,
  AxiosHeaders,
  type AxiosAdapter,
  type AxiosResponse,
  type InternalAxiosRequestConfig,
} from "axios";

vi.mock("@shared/ui/notifications", () => ({
  toast: {
    error: vi.fn(),
    success: vi.fn(),
  },
}));

vi.mock("../i18n/translateError", () => ({
  translateError: (namespace: string, key: string) => `${namespace}:${key}`,
}));

const refreshEnabledMock = vi.fn<() => boolean>();
const logoutLocalMock = vi.fn<() => void>();

vi.mock("../store/authStore", () => ({
  getRefreshEnabled: () => refreshEnabledMock(),
  useAuthStore: {
    getState: () => ({
      user: null,
      refreshEnabled: refreshEnabledMock(),
      logoutLocal: logoutLocalMock,
    }),
  },
}));

const {
  clearAccessToken,
  getAccessToken,
  httpClient,
  refreshAccessToken,
  setAccessToken,
} = await import("./httpClient");

const authSessionResponse = {
  data: {
    accessToken: "fresh",
    refreshToken: "refresh-token",
    user: {
      id: "user-1",
      createdAt: "2026-09-03T00:00:00Z",
      updatedAt: "2026-09-03T00:00:01Z",
      role: 1,
      username: "alice",
    },
  },
};

const buildAxiosError = (status: number): AxiosError => {
  const config: InternalAxiosRequestConfig = {
    headers: new AxiosHeaders(),
  };
  const response: AxiosResponse = {
    config,
    data: {},
    headers: {},
    status,
    statusText: "Request failed",
  };
  return new AxiosError(
    "Request failed",
    "ERR_BAD_RESPONSE",
    config,
    undefined,
    response,
  );
};

beforeEach(() => {
  refreshEnabledMock.mockReturnValue(true);
  logoutLocalMock.mockClear();
  setAccessToken("reset");
  clearAccessToken();
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("access token helpers", () => {
  it("round-trips token values through set, get, and clear", () => {
    expect(getAccessToken()).toBeNull();

    setAccessToken("abc");
    expect(getAccessToken()).toBe("abc");

    clearAccessToken();
    expect(getAccessToken()).toBeNull();
  });

  it("accepts null to clear the stored token", () => {
    setAccessToken("abc");
    setAccessToken(null);

    expect(getAccessToken()).toBeNull();
  });
});

describe("refreshAccessToken", () => {
  it("returns null and clears the token when refresh is disabled", async () => {
    refreshEnabledMock.mockReturnValue(false);
    setAccessToken("stale");

    await expect(refreshAccessToken()).resolves.toBeNull();
    expect(getAccessToken()).toBeNull();
  });

  it("allows one explicit refresh when local refresh is disabled", async () => {
    refreshEnabledMock.mockReturnValue(false);
    vi.spyOn(httpClient, "post").mockResolvedValue(authSessionResponse);

    await expect(
      refreshAccessToken({ allowWhenRefreshDisabled: true }),
    ).resolves.toBe("fresh");
    expect(getAccessToken()).toBe("fresh");
  });

  it("returns and stores the new token on success", async () => {
    vi.spyOn(httpClient, "post").mockResolvedValue(authSessionResponse);

    await expect(refreshAccessToken()).resolves.toBe("fresh");
    expect(getAccessToken()).toBe("fresh");
  });

  it("rejects a successful response with an invalid contract", async () => {
    vi.spyOn(httpClient, "post").mockResolvedValue({ data: {} });
    setAccessToken("stale");

    await expect(refreshAccessToken()).rejects.toThrow();
    expect(getAccessToken()).toBeNull();
  });

  it("logs out only when the refresh session is absent", async () => {
    vi.spyOn(httpClient, "post").mockRejectedValue(buildAxiosError(404));
    setAccessToken("stale");

    await expect(refreshAccessToken()).resolves.toBeNull();

    expect(getAccessToken()).toBeNull();
    expect(logoutLocalMock).toHaveBeenCalledTimes(1);
  });

  it.each([429, 500])(
    "keeps the local session when refresh fails with HTTP %i",
    async (status) => {
      vi.spyOn(httpClient, "post").mockRejectedValue(buildAxiosError(status));
      setAccessToken("stale");

      await expect(refreshAccessToken()).rejects.toMatchObject({
        response: { status },
      });

      expect(getAccessToken()).toBe("stale");
      expect(logoutLocalMock).not.toHaveBeenCalled();
    },
  );

  it("keeps the local session when the refresh request cannot reach the server", async () => {
    const networkError = new AxiosError("Network Error", "ERR_NETWORK");
    vi.spyOn(httpClient, "post").mockRejectedValue(networkError);
    setAccessToken("stale");

    await expect(refreshAccessToken()).rejects.toBe(networkError);

    expect(getAccessToken()).toBe("stale");
    expect(logoutLocalMock).not.toHaveBeenCalled();
  });

  it("shares one in-flight refresh request", async () => {
    let resolveRequest: ((value: object) => void) | undefined;
    const post = vi.spyOn(httpClient, "post").mockImplementation(
      () =>
        new Promise((resolve) => {
          resolveRequest = resolve;
        }),
    );

    const first = refreshAccessToken();
    const second = refreshAccessToken();

    expect(post).toHaveBeenCalledTimes(1);
    resolveRequest?.(authSessionResponse);

    await expect(Promise.all([first, second])).resolves.toEqual([
      "fresh",
      "fresh",
    ]);
  });

  it("does not restore an access token after local auth was cleared", async () => {
    let resolveRequest: ((value: object) => void) | undefined;
    vi.spyOn(httpClient, "post").mockImplementation(
      () =>
        new Promise((resolve) => {
          resolveRequest = resolve;
        }),
    );

    const refresh = refreshAccessToken();
    clearAccessToken();
    resolveRequest?.(authSessionResponse);

    await expect(refresh).resolves.toBeNull();
    expect(getAccessToken()).toBeNull();
  });
});

describe("401 response recovery", () => {
  const createResponse = (
    config: Parameters<AxiosAdapter>[0],
    status: number,
    data: object,
  ): AxiosResponse => ({
    config,
    data,
    headers: {},
    status,
    statusText: status === 200 ? "OK" : "Unauthorized",
  });

  it("retries the original request after a successful refresh", async () => {
    let requestCount = 0;
    const adapter: AxiosAdapter = async (config) => {
      requestCount += 1;
      if (requestCount === 1) {
        const response = createResponse(config, 401, {});
        throw new AxiosError(
          "Unauthorized",
          "ERR_BAD_RESPONSE",
          config,
          undefined,
          response,
        );
      }

      return createResponse(config, 200, { ok: true });
    };
    const refresh = vi
      .spyOn(httpClient, "post")
      .mockResolvedValue(authSessionResponse);

    await expect(
      httpClient.get("protected", { adapter }),
    ).resolves.toMatchObject({ data: { ok: true } });
    expect(requestCount).toBe(2);
    expect(refresh).toHaveBeenCalledTimes(1);
  });

  it("preserves auth state and rejects the original error after a temporary refresh failure", async () => {
    const adapter: AxiosAdapter = async (config) => {
      const response = createResponse(config, 401, {});
      throw new AxiosError(
        "Unauthorized",
        "ERR_BAD_RESPONSE",
        config,
        undefined,
        response,
      );
    };
    vi.spyOn(httpClient, "post").mockRejectedValue(buildAxiosError(500));
    setAccessToken("expired");

    await expect(
      httpClient.get("protected", { adapter }),
    ).rejects.toMatchObject({ response: { status: 401 } });
    expect(getAccessToken()).toBe("expired");
    expect(logoutLocalMock).not.toHaveBeenCalled();
  });
});
