import { renderHook, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { MAGIC } from "../../../shared/crypto/container";
import { useShareFileInfo } from "./useShareFileInfo";

describe("useShareFileInfo", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("marks opaque octet-stream shares as encrypted when the stream starts with a Cotton encrypted container", async () => {
    const fetchMock = vi.fn(async (_url: RequestInfo | URL, init?: RequestInit) => {
      if (init?.method === "HEAD") {
        return new Response(null, {
          status: 200,
          headers: {
            "content-disposition":
              "attachment; filename=\"6f7474ab-1e2b-4bbf-ae87-b0243d9acb33\"",
            "content-length": "128",
            "content-type": "application/octet-stream",
          },
        });
      }

      if (init?.method === "GET") {
        return new Response(MAGIC, { status: 206 });
      }

      throw new Error("Unexpected request");
    });
    vi.stubGlobal("fetch", fetchMock);

    const { result } = renderHook(() =>
      useShareFileInfo({
        token: "share-token",
        inlineUrl: "/s/share-token?view=inline",
        downloadUrl: "/s/share-token?view=download",
      }),
    );

    await waitFor(() => expect(result.current.loading).toBe(false));

    expect(result.current.error).toBeNull();
    expect(result.current.fileName).toBe(
      "6f7474ab-1e2b-4bbf-ae87-b0243d9acb33",
    );
    expect(result.current.encryptedContainer).toBe(true);
    expect(result.current.textContent).toBeNull();
    expect(fetchMock).toHaveBeenCalledWith("/s/share-token?view=inline", {
      method: "GET",
      headers: { Range: "bytes=0-3" },
    });
  });

  it("does not range-probe ordinary octet-stream shares", async () => {
    const fetchMock = vi.fn(async (_url: RequestInfo | URL, init?: RequestInit) => {
      if (init?.method === "HEAD") {
        return new Response(null, {
          status: 200,
          headers: {
            "content-disposition": "attachment; filename=\"archive.bin\"",
            "content-length": "128",
            "content-type": "application/octet-stream",
          },
        });
      }

      throw new Error("Unexpected request");
    });
    vi.stubGlobal("fetch", fetchMock);

    const { result } = renderHook(() =>
      useShareFileInfo({
        token: "share-token",
        inlineUrl: "/s/share-token?view=inline",
        downloadUrl: "/s/share-token?view=download",
      }),
    );

    await waitFor(() => expect(result.current.loading).toBe(false));

    expect(result.current.error).toBeNull();
    expect(result.current.fileName).toBe("archive.bin");
    expect(result.current.encryptedContainer).toBe(false);
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});
