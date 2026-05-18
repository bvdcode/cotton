import type { AxiosResponse } from "axios";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("react-toastify", () => ({
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
const { filesApi } = await import("./filesApi");

const fileId = "file-1";
const nodeId = "node-1";

const chunkRequest = {
  nodeId,
  chunkHashes: ["chunk-1", "chunk-2"],
  name: "document.pdf",
  contentType: "application/pdf",
  hash: "file-hash",
  originalNodeFileId: null,
};

beforeEach(() => {
  vi.spyOn(console, "error").mockImplementation(() => undefined);
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("filesApi chunk manifests", () => {
  it("creates a file from an ordered chunk manifest", async () => {
    const post = vi.spyOn(httpClient, "post").mockResolvedValue({
      data: undefined,
    });

    await filesApi.createFromChunks(chunkRequest);

    expect(post).toHaveBeenCalledWith("files/from-chunks", chunkRequest);
  });

  it("updates file content through the content update endpoint", async () => {
    const patch = vi.spyOn(httpClient, "patch").mockResolvedValue({
      data: undefined,
    });

    await filesApi.updateFileContent(fileId, chunkRequest);

    expect(patch).toHaveBeenCalledWith(
      `files/${fileId}/update-content`,
      chunkRequest,
    );
  });
});

describe("filesApi.getDownloadLink", () => {
  it("uses the default expiration and returns the response URL", async () => {
    const get = vi.spyOn(httpClient, "get").mockResolvedValue({
      data: "https://download.example/file",
    });

    await expect(filesApi.getDownloadLink(fileId)).resolves.toBe(
      "https://download.example/file",
    );

    expect(get).toHaveBeenCalledWith(`files/${fileId}/download-link`, {
      params: { expireAfterMinutes: 1440 },
    });
  });

  it("forwards explicit expiration minutes", async () => {
    const get = vi.spyOn(httpClient, "get").mockResolvedValue({
      data: "https://download.example/file",
    });

    await filesApi.getDownloadLink(fileId, 60);

    expect(get).toHaveBeenCalledWith(`files/${fileId}/download-link`, {
      params: { expireAfterMinutes: 60 },
    });
  });

  it("deduplicates concurrent calls for the same file and expiration", async () => {
    let resolve!: (response: AxiosResponse<string>) => void;
    const get = vi.spyOn(httpClient, "get").mockReturnValue(
      new Promise<AxiosResponse<string>>((resolvePromise) => {
        resolve = resolvePromise;
      }),
    );

    const first = filesApi.getDownloadLink(fileId, 1440);
    const second = filesApi.getDownloadLink(fileId, 1440);

    resolve({ data: "https://download.example/file" } as AxiosResponse<string>);

    await expect(Promise.all([first, second])).resolves.toEqual([
      "https://download.example/file",
      "https://download.example/file",
    ]);
    expect(get).toHaveBeenCalledTimes(1);
  });

  it("requests again after the previous in-flight call settles", async () => {
    const get = vi.spyOn(httpClient, "get").mockResolvedValue({
      data: "https://download.example/file",
    });

    await filesApi.getDownloadLink(fileId, 1440);
    await filesApi.getDownloadLink(fileId, 1440);

    expect(get).toHaveBeenCalledTimes(2);
  });

  it("keeps separate in-flight requests for different expiration values", async () => {
    const get = vi.spyOn(httpClient, "get").mockResolvedValue({
      data: "https://download.example/file",
    });

    await Promise.all([
      filesApi.getDownloadLink(fileId, 60),
      filesApi.getDownloadLink(fileId, 120),
    ]);

    expect(get).toHaveBeenCalledTimes(2);
  });
});

describe("filesApi file mutations", () => {
  it("deletes through trash by default", async () => {
    const del = vi.spyOn(httpClient, "delete").mockResolvedValue({
      data: undefined,
    });

    await filesApi.deleteFile(fileId);

    expect(del).toHaveBeenCalledWith(`files/${fileId}`, {
      params: undefined,
    });
  });

  it("can request permanent deletion", async () => {
    const del = vi.spyOn(httpClient, "delete").mockResolvedValue({
      data: undefined,
    });

    await filesApi.deleteFile(fileId, true);

    expect(del).toHaveBeenCalledWith(`files/${fileId}`, {
      params: { skipTrash: true },
    });
  });

  it("renames and moves files through the scoped mutation endpoints", async () => {
    const patch = vi.spyOn(httpClient, "patch").mockResolvedValue({
      data: undefined,
    });

    await filesApi.renameFile(fileId, { name: "renamed.txt" });
    await filesApi.moveFile(fileId, { parentId: nodeId });

    expect(patch).toHaveBeenNthCalledWith(1, `/files/${fileId}/rename`, {
      name: "renamed.txt",
    });
    expect(patch).toHaveBeenNthCalledWith(2, `/files/${fileId}/move`, {
      parentId: nodeId,
    });
  });

  it("updates file metadata through the scoped metadata endpoint", async () => {
    const response = {
      id: fileId,
      createdAt: "2026-05-17T00:00:00Z",
      updatedAt: "2026-05-17T00:00:01Z",
      nodeId,
      ownerId: "owner-1",
      name: "encrypted.bin",
      contentType: "application/octet-stream",
      sizeBytes: 12,
      metadata: { en: "encrypted-display-name" },
      requiresVideoTranscoding: false,
      previewHashEncryptedHex: null,
    };
    const patch = vi.spyOn(httpClient, "patch").mockResolvedValue({
      data: response,
    });

    await expect(
      filesApi.updateFileMetadata(fileId, { en: "encrypted-display-name" }),
    ).resolves.toEqual(response);

    expect(patch).toHaveBeenCalledWith(`/files/${fileId}/metadata`, {
      en: "encrypted-display-name",
    });
  });

  it("restores files with stable default conflict options", async () => {
    const post = vi.spyOn(httpClient, "post").mockResolvedValue({
      data: { status: "Restored" },
    });

    await expect(filesApi.restoreFile(fileId)).resolves.toEqual({
      status: "Restored",
    });

    expect(post).toHaveBeenCalledWith(`/files/${fileId}/restore`, {
      createMissingParents: false,
      overwrite: false,
    });
  });

  it("passes explicit restore conflict options", async () => {
    const post = vi.spyOn(httpClient, "post").mockResolvedValue({
      data: { status: "Restored" },
    });

    await filesApi.restoreFile(fileId, {
      createMissingParents: true,
      overwrite: true,
    });

    expect(post).toHaveBeenCalledWith(`/files/${fileId}/restore`, {
      createMissingParents: true,
      overwrite: true,
    });
  });
});
