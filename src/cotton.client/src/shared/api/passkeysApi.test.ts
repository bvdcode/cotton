import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const { clearAccessToken, getAccessToken, httpClient } =
  await import("./httpClient");
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
      data: [{ id: "credential-1", label: "YubiKey" }],
    });

    await expect(passkeysApi.list()).resolves.toEqual([
      { id: "credential-1", label: "YubiKey" },
    ]);
    expect(httpClient.get).toHaveBeenCalledWith("auth/passkeys");
  });

  it("sets a passkey label", async () => {
    const put = vi.spyOn(httpClient, "put").mockResolvedValue({
      data: { id: "credential-1", label: "YubiKey" },
    });

    await expect(
      passkeysApi.setLabel("credential-1", "  YubiKey  "),
    ).resolves.toEqual({ id: "credential-1", label: "YubiKey" });
    expect(put).toHaveBeenCalledWith("auth/passkeys/credential-1", {
      label: "YubiKey",
    });
  });

  it("clears a passkey label", async () => {
    const put = vi.spyOn(httpClient, "put").mockResolvedValue({
      data: { id: "credential-1", label: null },
    });

    await expect(passkeysApi.setLabel("credential-1", "   ")).resolves.toEqual({
      id: "credential-1",
      label: null,
    });
    expect(put).toHaveBeenCalledWith("auth/passkeys/credential-1", {
      label: null,
    });
  });

  it("registers a passkey without a user label", async () => {
    const post = vi.spyOn(httpClient, "post").mockResolvedValue({
      data: { id: "credential-1", label: null },
    });

    await expect(
      passkeysApi.finishRegistration("request-id", null, {
        id: "credential-id",
        rawId: "credential-id",
        type: "public-key",
        transports: ["internal"],
        response: {
          attestationObject: "attestation-object",
          clientDataJson: "client-data",
        },
      }),
    ).resolves.toEqual({
      id: "credential-1",
      label: null,
    });
    expect(post).toHaveBeenCalledWith("auth/passkeys/registration/verify", {
      requestId: "request-id",
      label: null,
      credential: {
        id: "credential-id",
        rawId: "credential-id",
        type: "public-key",
        transports: ["internal"],
        response: {
          attestationObject: "attestation-object",
          clientDataJson: "client-data",
        },
      },
    });
  });

  it("trims a custom user label during passkey registration", async () => {
    const post = vi.spyOn(httpClient, "post").mockResolvedValue({
      data: { id: "credential-1", label: "Office key" },
    });
    const credential = {
      id: "credential-id",
      rawId: "credential-id",
      type: "public-key" as const,
      transports: ["internal"],
      response: {
        attestationObject: "attestation-object",
        clientDataJson: "client-data",
      },
    };

    await passkeysApi.finishRegistration(
      "request-id",
      "  Office key  ",
      credential,
    );

    expect(post).toHaveBeenCalledWith("auth/passkeys/registration/verify", {
      requestId: "request-id",
      label: "Office key",
      credential,
    });
  });

  it("stores the access token returned by passkey assertion verification", async () => {
    vi.spyOn(httpClient, "post").mockResolvedValue({
      data: { accessToken: "passkey-token" },
    });

    const token = await passkeysApi.finishAssertion("request-id", true, {
      id: "credential-id",
      rawId: "credential-id",
      type: "public-key",
      response: {
        authenticatorData: "authenticator-data",
        clientDataJson: "client-data",
        signature: "signature",
        userHandle: null,
      },
    });

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
