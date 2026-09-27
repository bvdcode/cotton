import type { AxiosProgressEvent } from "axios";
import {
  createHttpResponse,
  createUploadProgress,
} from "../../test/httpFixtures";
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
const { chunksApi } = await import("./chunksApi");

afterEach(() => {
  vi.restoreAllMocks();
});

describe("chunksApi.exists", () => {
  it("returns the server boolean for existing chunks", async () => {
    const get = vi.spyOn(httpClient, "get").mockResolvedValue({
      status: 200,
      data: true,
    });

    await expect(chunksApi.exists("abc")).resolves.toBe(true);

    expect(get).toHaveBeenCalledWith(
      "chunks/abc/exists",
      expect.objectContaining({
        validateStatus: expect.any(Function),
      }),
    );
  });

  it("URL-encodes hash path segments", async () => {
    const get = vi.spyOn(httpClient, "get").mockResolvedValue({
      status: 200,
      data: true,
    });

    await chunksApi.exists("a/b+c=");

    expect(get.mock.calls[0][0]).toBe("chunks/a%2Fb%2Bc%3D/exists");
  });

  it("treats 404 as a missing chunk", async () => {
    vi.spyOn(httpClient, "get").mockResolvedValue({
      status: 404,
      data: null,
    });

    await expect(chunksApi.exists("missing")).resolves.toBe(false);
  });

  it("forwards the abort signal", async () => {
    const get = vi.spyOn(httpClient, "get").mockResolvedValue({
      status: 200,
      data: true,
    });
    const controller = new AbortController();

    await chunksApi.exists("abc", controller.signal);

    expect(get).toHaveBeenCalledWith(
      "chunks/abc/exists",
      expect.objectContaining({ signal: controller.signal }),
    );
  });

  it("only treats 200 and 404 as handled responses", async () => {
    const get = vi.spyOn(httpClient, "get").mockResolvedValue({
      status: 200,
      data: true,
    });

    await chunksApi.exists("abc");

    const validateStatus = get.mock.calls[0][1]?.validateStatus;
    expect(validateStatus?.(200)).toBe(true);
    expect(validateStatus?.(404)).toBe(true);
    expect(validateStatus?.(500)).toBe(false);
  });
});

describe("chunksApi.uploadChunk", () => {
  const makeBlob = () => new Blob(["chunk-bytes"], { type: "text/plain" });

  it("posts a raw chunk body with the hash in query params", async () => {
    const post = vi.spyOn(httpClient, "post").mockResolvedValue({
      data: undefined,
    });
    const blob = makeBlob();

    await chunksApi.uploadChunk({
      blob,
      fileName: "chunk.bin",
      hash: "chunk-hash",
    });

    const [url, body, config] = post.mock.calls[0];
    expect(url).toBe("chunks/raw");
    expect(body).toBe(blob);
    expect(config?.params).toEqual({ hash: "chunk-hash" });
    expect(config?.headers?.["Content-Type"]).toBe("application/octet-stream");
  });

  it("requires a hash for raw chunk uploads", async () => {
    const post = vi.spyOn(httpClient, "post").mockResolvedValue({
      data: undefined,
    });

    await expect(
      chunksApi.uploadChunk({
        blob: makeBlob(),
        fileName: "without-null-hash.bin",
        hash: null,
      }),
    ).rejects.toThrow("Chunk hash is required for raw chunk uploads.");
    await expect(
      chunksApi.uploadChunk({
        blob: makeBlob(),
        fileName: "without-undefined-hash.bin",
      }),
    ).rejects.toThrow("Chunk hash is required for raw chunk uploads.");

    expect(post).not.toHaveBeenCalled();
  });

  it("forwards the abort signal to the upload request", async () => {
    const post = vi.spyOn(httpClient, "post").mockResolvedValue({
      data: undefined,
    });
    const controller = new AbortController();

    await chunksApi.uploadChunk({
      blob: makeBlob(),
      fileName: "chunk.bin",
      hash: "chunk-hash",
      signal: controller.signal,
    });

    expect(post.mock.calls[0][2]).toEqual(
      expect.objectContaining({ signal: controller.signal }),
    );
  });

  it("reports upload progress scaled and clamped to blob bytes", async () => {
    const blob = makeBlob();
    const onProgress = vi.fn();
    let progressCallback: ((event: AxiosProgressEvent) => void) | undefined;

    vi.spyOn(httpClient, "post").mockImplementation((_url, _data, config) => {
      progressCallback = config?.onUploadProgress;
      return Promise.resolve(createHttpResponse(undefined));
    });

    await chunksApi.uploadChunk({
      blob,
      fileName: "chunk.bin",
      hash: "chunk-hash",
      onProgress,
    });

    progressCallback?.(createUploadProgress(50, 100));
    progressCallback?.(createUploadProgress(500, 100));
    progressCallback?.(createUploadProgress(4));

    expect(onProgress).toHaveBeenNthCalledWith(1, Math.floor(blob.size * 0.5));
    expect(onProgress).toHaveBeenNthCalledWith(2, blob.size);
    expect(onProgress).toHaveBeenNthCalledWith(3, 4);
  });

  it("does not require a progress callback", async () => {
    const post = vi.spyOn(httpClient, "post").mockResolvedValue({
      data: undefined,
    });

    await expect(
      chunksApi.uploadChunk({
        blob: makeBlob(),
        fileName: "chunk.bin",
        hash: "chunk-hash",
      }),
    ).resolves.toBeUndefined();

    const config = post.mock.calls[0][2];
    expect(() =>
      config?.onUploadProgress?.(createUploadProgress(1)),
    ).not.toThrow();
  });
});
