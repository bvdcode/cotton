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
const { layoutsApi } = await import("./layoutsApi");
const {
  DISPLAY_META_KEY,
  ENCRYPTED_FLAG_KEY,
  encryptDisplayMeta,
  generateMasterKey,
  useVault,
} = await import("../crypto");

const layoutId = "layout-1";
const nodeId = "node-1";

afterEach(() => {
  vi.restoreAllMocks();
  useVault.getState().lock();
});

describe("layoutsApi.resolve", () => {
  it("uses the bare resolver endpoint when path is absent or blank", async () => {
    const get = vi.spyOn(httpClient, "get").mockResolvedValue({
      data: { id: nodeId },
    });

    await layoutsApi.resolve();
    expect(get).toHaveBeenLastCalledWith("layouts/resolver", {
      params: undefined,
    });

    await layoutsApi.resolve({ path: "   " });
    expect(get).toHaveBeenLastCalledWith("layouts/resolver", {
      params: undefined,
    });
  });

  it("forwards nodeType on the bare resolver endpoint", async () => {
    const get = vi.spyOn(httpClient, "get").mockResolvedValue({
      data: { id: nodeId },
    });

    await layoutsApi.resolve({ nodeType: "trash" });

    expect(get).toHaveBeenCalledWith("layouts/resolver", {
      params: { nodeType: "trash" },
    });
  });

  it("encodes each path segment while preserving slash separators", async () => {
    const get = vi.spyOn(httpClient, "get").mockResolvedValue({
      data: { id: nodeId },
    });

    await layoutsApi.resolve({ path: "/Docs/My folder/file (1).txt" });

    expect(get).toHaveBeenCalledWith(
      "/layouts/resolver/Docs/My%20folder/file%20(1).txt",
    );
  });
});

describe("layoutsApi reads", () => {
  it("gets stats for a layout", async () => {
    const get = vi.spyOn(httpClient, "get").mockResolvedValue({
      data: { layoutId, nodeCount: 1, fileCount: 2, sizeBytes: 10 },
    });

    const stats = await layoutsApi.getStats(layoutId);

    expect(get).toHaveBeenCalledWith(`/layouts/${layoutId}/stats`);
    expect(stats.fileCount).toBe(2);
  });

  it("searches with query and pagination and reads total-count", async () => {
    const get = vi.spyOn(httpClient, "get").mockResolvedValue({
      data: { nodes: [], files: [], nodePaths: {}, filePaths: {} },
      headers: { "x-total-count": "17" },
    });

    const result = await layoutsApi.search({
      layoutId,
      query: "thing",
      page: 2,
      pageSize: 50,
    });

    expect(get).toHaveBeenCalledWith(`/layouts/${layoutId}/search`, {
      params: { query: "thing", page: 2, pageSize: 50 },
    });
    expect(result.totalCount).toBe(17);
  });

  it("decorates encrypted search result file names when the vault is unlocked", async () => {
    useVault.getState().unlock(await generateMasterKey());
    const encryptedMeta = await encryptDisplayMeta({
      name: "private.pdf",
      contentType: "application/pdf",
    });
    vi.spyOn(httpClient, "get").mockResolvedValue({
      data: {
        nodes: [],
        files: [
          {
            id: "file-1",
            createdAt: "2026-05-17T00:00:00Z",
            updatedAt: "2026-05-17T00:00:00Z",
            nodeId,
            ownerId: "owner-1",
            name: "11111111-2222-4333-8444-555555555555",
            contentType: "application/octet-stream",
            sizeBytes: 10,
            metadata: {
              [ENCRYPTED_FLAG_KEY]: "true",
              [DISPLAY_META_KEY]: encryptedMeta,
            },
          },
        ],
        nodePaths: {},
        filePaths: {},
      },
      headers: { "x-total-count": "1" },
    });

    const result = await layoutsApi.search({ layoutId, query: "private" });

    expect(result.data.files[0]).toMatchObject({
      name: "private.pdf",
      contentType: "application/pdf",
    });
  });

  it("defaults search total-count to zero when header is missing", async () => {
    vi.spyOn(httpClient, "get").mockResolvedValue({
      data: { nodes: [], files: [], nodePaths: {}, filePaths: {} },
      headers: {},
    });

    const result = await layoutsApi.search({ layoutId, query: "thing" });

    expect(result.totalCount).toBe(0);
  });

  it("gets share links and recent files with default params", async () => {
    const get = vi.spyOn(httpClient, "get").mockResolvedValueOnce({
      data: "https://share/node",
    }).mockResolvedValueOnce({
      data: [],
    });

    await expect(layoutsApi.getNodeShareLink(nodeId)).resolves.toBe(
      "https://share/node",
    );
    expect(get).toHaveBeenLastCalledWith(`/layouts/nodes/${nodeId}/share-link`, {
      params: { expireAfterMinutes: 1440 },
    });

    await expect(layoutsApi.getRecentFiles(layoutId)).resolves.toEqual([]);
    expect(get).toHaveBeenLastCalledWith(`/layouts/${layoutId}/recent`, {
      params: { count: 3 },
    });
  });

  it("threads explicit share-link and recent-file params", async () => {
    const get = vi.spyOn(httpClient, "get").mockResolvedValue({ data: [] });

    await layoutsApi.getNodeShareLink(nodeId, 60);
    expect(get).toHaveBeenLastCalledWith(`/layouts/nodes/${nodeId}/share-link`, {
      params: { expireAfterMinutes: 60 },
    });

    await layoutsApi.getRecentFiles(layoutId, 10);
    expect(get).toHaveBeenLastCalledWith(`/layouts/${layoutId}/recent`, {
      params: { count: 10 },
    });
  });
});
