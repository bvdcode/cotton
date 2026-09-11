import { afterEach, describe, expect, it, vi } from "vitest";
import { httpClient } from "./httpClient";
import { startupApi } from "./startupApi";

afterEach(() => {
  vi.restoreAllMocks();
});

describe("startupApi", () => {
  it("shares one in-flight status request", async () => {
    let resolveRequest: ((value: object) => void) | undefined;
    const get = vi.spyOn(httpClient, "get").mockImplementation(
      () =>
        new Promise((resolve) => {
          resolveRequest = resolve;
        }),
    );

    const first = startupApi.getStatus();
    const second = startupApi.getStatus();

    expect(get).toHaveBeenCalledTimes(1);
    resolveRequest?.({ data: { blocked: false } });

    await expect(Promise.all([first, second])).resolves.toEqual([
      { blocked: false },
      { blocked: false },
    ]);
  });

  it("allows a retry after a failed request", async () => {
    const get = vi
      .spyOn(httpClient, "get")
      .mockRejectedValueOnce(new Error("offline"))
      .mockResolvedValueOnce({ data: { blocked: false } });

    await expect(startupApi.getStatus()).rejects.toThrow("offline");
    await expect(startupApi.getStatus()).resolves.toEqual({ blocked: false });
    expect(get).toHaveBeenCalledTimes(2);
  });
});
