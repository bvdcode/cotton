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
const { nodesApi } = await import("./nodesApi");
const {
  DISPLAY_META_KEY,
  ENCRYPTED_FLAG_KEY,
  encryptDisplayMeta,
  generateMasterKey,
  useVault,
} = await import("../crypto");

const nodeId = "node-1";
const parentId = "parent-1";
const layoutId = "layout-1";

const makeNode = (id = nodeId) => ({
  id,
  createdAt: "2026-05-17T00:00:00Z",
  updatedAt: "2026-05-17T00:00:00Z",
  layoutId,
  parentId: null,
  name: `Folder ${id}`,
  metadata: {},
});

const makeContent = (files: unknown[] = []) => ({
  id: nodeId,
  createdAt: "2026-05-17T00:00:00Z",
  updatedAt: "2026-05-17T00:00:00Z",
  nodes: [makeNode("child-1")],
  files,
});

beforeEach(() => {
  vi.spyOn(console, "error").mockImplementation(() => undefined);
});

afterEach(() => {
  vi.restoreAllMocks();
  useVault.getState().lock();
});

describe("nodesApi reads", () => {
  it("gets one node and validates the body", async () => {
    const get = vi.spyOn(httpClient, "get").mockResolvedValue({
      data: makeNode(),
    });

    const node = await nodesApi.getNode(nodeId);

    expect(get).toHaveBeenCalledWith(`/layouts/nodes/${nodeId}`, undefined);
    expect(node.id).toBe(nodeId);
  });

  it("gets ancestors with optional nodeType", async () => {
    const get = vi.spyOn(httpClient, "get").mockResolvedValue({
      data: [makeNode(parentId)],
    });

    await nodesApi.getAncestors(nodeId);
    expect(get).toHaveBeenLastCalledWith(`/layouts/nodes/${nodeId}/ancestors`, {
      params: undefined,
    });

    await nodesApi.getAncestors(nodeId, { nodeType: "trash" });
    expect(get).toHaveBeenLastCalledWith(`/layouts/nodes/${nodeId}/ancestors`, {
      params: { nodeType: "trash" },
    });
  });

  it("gets children with default pagination and required total-count header", async () => {
    const get = vi.spyOn(httpClient, "get").mockResolvedValue({
      data: makeContent(),
      headers: { "x-total-count": "5" },
    });

    const result = await nodesApi.getChildren(nodeId);

    expect(get).toHaveBeenCalledWith(`/layouts/nodes/${nodeId}/children`, {
      params: {
        page: 1,
        pageSize: 1000000,
        nodeType: undefined,
        depth: undefined,
      },
    });
    expect(result.content.nodes[0].id).toBe("child-1");
    expect(result.totalCount).toBe(5);
  });

  it("decorates encrypted child file names when the vault is unlocked", async () => {
    useVault.getState().unlock(await generateMasterKey());
    const encryptedMeta = await encryptDisplayMeta({
      name: "private.pdf",
      contentType: "application/pdf",
    });
    vi.spyOn(httpClient, "get").mockResolvedValue({
      data: makeContent([
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
      ]),
      headers: { "x-total-count": "1" },
    });

    const result = await nodesApi.getChildren(nodeId);

    expect(result.content.files[0]).toMatchObject({
      name: "private.pdf",
      contentType: "application/pdf",
    });
  });

  it("threads nodeType, pagination, and depth into children request", async () => {
    const get = vi.spyOn(httpClient, "get").mockResolvedValue({
      data: makeContent(),
      headers: { "x-total-count": "1" },
    });

    await nodesApi.getChildren(nodeId, {
      nodeType: "trash",
      page: 3,
      pageSize: 50,
      depth: 2,
    });

    expect(get).toHaveBeenCalledWith(`/layouts/nodes/${nodeId}/children`, {
      params: {
        page: 3,
        pageSize: 50,
        nodeType: "trash",
        depth: 2,
      },
    });
  });
});

describe("nodesApi mutations", () => {
  it("creates a node via PUT and validates the response", async () => {
    const put = vi.spyOn(httpClient, "put").mockResolvedValue({
      data: makeNode("created"),
    });

    const result = await nodesApi.createNode({ parentId, name: "New" });

    expect(put).toHaveBeenCalledWith("layouts/nodes", {
      parentId,
      name: "New",
    });
    expect(result.id).toBe("created");
  });

  it("deletes with skipTrash only when requested", async () => {
    const del = vi.spyOn(httpClient, "delete").mockResolvedValue({});

    await nodesApi.deleteNode(nodeId);
    expect(del).toHaveBeenLastCalledWith(`/layouts/nodes/${nodeId}`, {
      params: undefined,
    });

    await nodesApi.deleteNode(nodeId, true);
    expect(del).toHaveBeenLastCalledWith(`/layouts/nodes/${nodeId}`, {
      params: { skipTrash: true },
    });
  });

  it("renames, moves, and restores nodes through the expected endpoints", async () => {
    const patch = vi.spyOn(httpClient, "patch").mockResolvedValue({
      data: makeNode(),
    });
    const post = vi.spyOn(httpClient, "post").mockResolvedValue({
      data: { status: "Restored" },
    });

    await nodesApi.renameNode(nodeId, { name: "Renamed" });
    expect(patch).toHaveBeenCalledWith(`/layouts/nodes/${nodeId}/rename`, {
      name: "Renamed",
    });

    await nodesApi.moveNode(nodeId, { parentId });
    expect(patch).toHaveBeenCalledWith(`/layouts/nodes/${nodeId}/move`, {
      parentId,
    });

    await nodesApi.updateNodeMetadata(nodeId, {
      isClientEncryptionEnabled: "true",
    });
    expect(patch).toHaveBeenCalledWith(`/layouts/nodes/${nodeId}/metadata`, {
      isClientEncryptionEnabled: "true",
    });

    const outcome = await nodesApi.restoreNode(nodeId, {
      createMissingParents: true,
      overwrite: true,
    });
    expect(post).toHaveBeenCalledWith(`/layouts/nodes/${nodeId}/restore`, {
      createMissingParents: true,
      overwrite: true,
    });
    expect(outcome.status).toBe("Restored");
  });

  it("defaults restore options to false", async () => {
    const post = vi.spyOn(httpClient, "post").mockResolvedValue({
      data: { status: "NotRestorable", reason: "Already active" },
    });

    await nodesApi.restoreNode(nodeId);

    expect(post).toHaveBeenCalledWith(`/layouts/nodes/${nodeId}/restore`, {
      createMissingParents: false,
      overwrite: false,
    });
  });
});
