import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const {
  clearAccessToken,
  getAccessToken,
  httpClient,
} = await import("./httpClient");
const { passkeysApi } = await import("./passkeysApi");

beforeEach(() => {
  clearAccessToken();
});

afterEach(() => {
  vi.restoreAllMocks();
  clearAccessToken();
});

describe("passkeysApi", () => {
  it("loads registered passkeys", async () => {
    vi.spyOn(httpClient, "get").mockResolvedValue({
      data: [{ id: "credential-1", name: "YubiKey" }],
    });

    await expect(passkeysApi.list()).resolves.toEqual([
      { id: "credential-1", name: "YubiKey" },
    ]);
    expect(httpClient.get).toHaveBeenCalledWith("auth/passkeys");
  });

  it("stores the access token returned by passkey assertion verification", async () => {
    vi.spyOn(httpClient, "post").mockResolvedValue({
      data: { accessToken: "passkey-token" },
    });

    const token = await passkeysApi.finishAssertion(
      "request-id",
      true,
      {
        id: "credential-id",
        rawId: "credential-id",
        type: "public-key",
        response: {
          authenticatorData: "authenticator-data",
          clientDataJson: "client-data",
          signature: "signature",
          userHandle: null,
        },
      },
    );

    expect(token).toBe("passkey-token");
    expect(getAccessToken()).toBe("passkey-token");
    expect(httpClient.post).toHaveBeenCalledWith(
      "auth/passkeys/assertion/verify",
      {
        requestId: "request-id",
        trustDevice: true,
        credential: {
          id: "credential-id",
          rawId: "credential-id",
          type: "public-key",
          response: {
            authenticatorData: "authenticator-data",
            clientDataJson: "client-data",
            signature: "signature",
            userHandle: null,
          },
        },
      },
    );
  });
});
