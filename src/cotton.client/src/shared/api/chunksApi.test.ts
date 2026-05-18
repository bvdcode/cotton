import type { AxiosProgressEvent, AxiosResponse } from "axios";
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
const { chunksApi } = await import("./chunksApi");

beforeEach(() => {
  vi.spyOn(console, "error").mockImplementation(() => undefined);
});

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

    const config = get.mock.calls[0][1] as {
      validateStatus: (status: number) => boolean;
    };
    expect(config.validateStatus(200)).toBe(true);
    expect(config.validateStatus(404)).toBe(true);
    expect(config.validateStatus(500)).toBe(false);
  });
});

describe("chunksApi.uploadChunk", () => {
  const makeBlob = () => new Blob(["chunk-bytes"], { type: "text/plain" });

  it("posts a multipart payload with file and hash fields", async () => {
    const post = vi.spyOn(httpClient, "post").mockResolvedValue({
      data: undefined,
    });

    await chunksApi.uploadChunk({
      blob: makeBlob(),
      fileName: "chunk.bin",
      hash: "chunk-hash",
    });

    const [url, body, config] = post.mock.calls[0] as [
      string,
      FormData,
      { headers: Record<string, string> },
    ];
    expect(url).toBe("chunks");
    expect(body).toBeInstanceOf(FormData);
    expect(body.get("file")).toBeInstanceOf(Blob);
    expect(body.get("hash")).toBe("chunk-hash");
    expect(config.headers["Content-Type"]).toBe("multipart/form-data");
  });

  it("omits hash validation when hash is null or undefined", async () => {
    const post = vi.spyOn(httpClient, "post").mockResolvedValue({
      data: undefined,
    });

    await chunksApi.uploadChunk({
      blob: makeBlob(),
      fileName: "without-null-hash.bin",
      hash: null,
    });
    await chunksApi.uploadChunk({
      blob: makeBlob(),
      fileName: "without-undefined-hash.bin",
    });

    expect((post.mock.calls[0][1] as FormData).has("hash")).toBe(false);
    expect((post.mock.calls[1][1] as FormData).has("hash")).toBe(false);
  });

  it("forwards the abort signal to the upload request", async () => {
    const post = vi.spyOn(httpClient, "post").mockResolvedValue({
      data: undefined,
    });
    const controller = new AbortController();

    await chunksApi.uploadChunk({
      blob: makeBlob(),
      fileName: "chunk.bin",
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
      return Promise.resolve({
        data: undefined,
      } as AxiosResponse<void>);
    });

    await chunksApi.uploadChunk({
      blob,
      fileName: "chunk.bin",
      onProgress,
    });

    progressCallback?.({ loaded: 50, total: 100 } as AxiosProgressEvent);
    progressCallback?.({ loaded: 500, total: 100 } as AxiosProgressEvent);
    progressCallback?.({ loaded: 4 } as AxiosProgressEvent);

    expect(onProgress).toHaveBeenNthCalledWith(
      1,
      Math.floor(blob.size * 0.5),
    );
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
      }),
    ).resolves.toBeUndefined();

    const config = post.mock.calls[0][2] as {
      onUploadProgress?: (event: AxiosProgressEvent) => void;
    };
    expect(() =>
      config.onUploadProgress?.({ loaded: 1 } as AxiosProgressEvent),
    ).not.toThrow();
  });
});
