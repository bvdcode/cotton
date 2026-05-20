import { afterEach, describe, expect, it, vi } from "vitest";
import { unlockApi } from "./unlockApi";

const jsonResponse = (status: number): Response =>
  new Response("{}", {
    status,
    headers: { "content-type": "application/json" },
  });

describe("unlockApi.waitUntilAppReady", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("waits until the main app server info endpoint is ready", async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockRejectedValueOnce(new TypeError("server restarting"))
      .mockResolvedValueOnce(jsonResponse(200));

    vi.stubGlobal("fetch", fetchMock);

    await expect(
      unlockApi.waitUntilAppReady({ timeoutMs: 1000, intervalMs: 1 }),
    ).resolves.toBe(true);

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(fetchMock).toHaveBeenLastCalledWith("/api/v1/server/info", {
      cache: "no-store",
      headers: { Accept: "application/json" },
    });
  });

  it("returns false when the app never becomes ready before timeout", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(423)),
    );

    await expect(
      unlockApi.waitUntilAppReady({ timeoutMs: 0, intervalMs: 0 }),
    ).resolves.toBe(false);
  });
});
