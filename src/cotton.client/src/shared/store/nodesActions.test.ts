import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("../api/nodesApi", () => ({
  nodesApi: {
    createNode: vi.fn(),
    deleteNode: vi.fn(),
    renameNode: vi.fn(),
    updateNodeMetadata: vi.fn(),
    getNode: vi.fn(),
    getAncestors: vi.fn(),
    getChildren: vi.fn(),
  },
}));

vi.mock("../api/layoutsApi", () => ({
  layoutsApi: {
    resolve: vi.fn(),
  },
}));

import type { NodeDto } from "../api/layoutsApi";
import type { NodeContentDto, NodeResponse } from "../api/nodesApi";
import { nodesApi } from "../api/nodesApi";
import {
  createFolder,
  deleteFolder,
  loadNode,
  renameFolder,
} from "./nodesActions";
import { resetNodesActionsInternals } from "./nodesActionInternals";
import { useNodesStore } from "./nodesStore";

const makeNode = (id: string, name: string): NodeDto => ({
  id,
  createdAt: "2026-05-13T00:00:00Z",
  updatedAt: "2026-05-13T00:00:00Z",
  layoutId: "layout-1",
  parentId: "parent-1",
  name,
  metadata: {},
});

const seedFolder = (parentId: string, nodes: NodeDto[]) => {
  const content: NodeContentDto = {
    id: parentId,
    createdAt: "2026-05-13T00:00:00Z",
    updatedAt: "2026-05-13T00:00:00Z",
    nodes,
    files: [],
  };
  useNodesStore.setState((prev) => ({
    ...prev,
    contentByNodeId: { ...prev.contentByNodeId, [parentId]: content },
    loading: false,
    error: null,
  }));
};

const resetStore = () => {
  resetNodesActionsInternals();
  useNodesStore.setState({
    cacheOwnerUserId: null,
    rootNodeId: null,
    currentNode: null,
    ancestors: [],
    contentByNodeId: {},
    ancestorsByNodeId: {},
    loading: false,
    error: null,
    lastUpdatedByNodeId: {},
  });
};

beforeEach(() => {
  resetStore();
  vi.clearAllMocks();
  vi.mocked(nodesApi.getChildren).mockImplementation(
    () => new Promise<NodeResponse>(() => {}),
  );
});

afterEach(() => {
  resetStore();
});

describe("loadNode", () => {
  it("force reload bypasses cached content and fetches a fresh node snapshot", async () => {
    const stale = makeNode("folder-1", "Old name");
    const fresh = makeNode("folder-1", "New name");
    const child = { ...makeNode("child-1", "Child"), parentId: "folder-1" };
    seedFolder("folder-1", []);
    useNodesStore.setState({ currentNode: stale, ancestors: [] });
    vi.mocked(nodesApi.getNode).mockResolvedValue(fresh);
    vi.mocked(nodesApi.getAncestors).mockResolvedValue([]);
    vi.mocked(nodesApi.getChildren).mockResolvedValue({
      content: {
        id: "folder-1",
        createdAt: "2026-05-13T00:00:00Z",
        updatedAt: "2026-05-13T00:00:00Z",
        nodes: [child],
        files: [],
      },
      totalCount: 1,
    });

    await loadNode("folder-1", { loadChildren: true, force: true });

    expect(nodesApi.getNode).toHaveBeenCalledWith("folder-1");
    expect(nodesApi.getAncestors).toHaveBeenCalledWith("folder-1");
    expect(nodesApi.getChildren).toHaveBeenCalledWith("folder-1", {
      page: 1,
      pageSize: 100000,
    });
    expect(useNodesStore.getState().currentNode?.name).toBe("New name");
    expect(
      useNodesStore.getState().contentByNodeId["folder-1"]?.nodes,
    ).toEqual([child]);
  });
});

describe("createFolder", () => {
  it("returns null and skips the API call when the name is empty", async () => {
    const result = await createFolder("parent-1", "   ");

    expect(result).toBeNull();
    expect(nodesApi.createNode).not.toHaveBeenCalled();
  });

  it("rejects duplicate names from cached siblings", async () => {
    seedFolder("parent-1", [makeNode("a", "Drafts")]);

    const result = await createFolder("parent-1", "drafts");

    expect(result).toBeNull();
    expect(nodesApi.createNode).not.toHaveBeenCalled();
    expect(useNodesStore.getState().error).toBe(
      "A folder with this name already exists",
    );
  });

  it("adds the new folder to the parent cache on success", async () => {
    seedFolder("parent-1", [makeNode("a", "Drafts")]);
    const created = makeNode("b", "Photos");
    vi.mocked(nodesApi.createNode).mockResolvedValue(created);

    const result = await createFolder("parent-1", "Photos");

    expect(result).toEqual(created);
    expect(nodesApi.createNode).toHaveBeenCalledWith({
      parentId: "parent-1",
      name: "Photos",
    });
    expect(
      useNodesStore
        .getState()
        .contentByNodeId["parent-1"]?.nodes.map((n) => n.name),
    ).toEqual(["Drafts", "Photos"]);
    expect(useNodesStore.getState().loading).toBe(false);
  });

  it("inherits the parent client-side encryption policy", async () => {
    const encryptedParent = {
      ...makeNode("parent-1", "Vault"),
      metadata: { isClientEncryptionEnabled: "true" },
    };
    useNodesStore.setState({ currentNode: encryptedParent });
    seedFolder("parent-1", []);
    const created = makeNode("b", "Private");
    const encryptedCreated = {
      ...created,
      metadata: { isClientEncryptionEnabled: "true" },
    };
    vi.mocked(nodesApi.createNode).mockResolvedValue(created);
    vi.mocked(nodesApi.updateNodeMetadata).mockResolvedValue(encryptedCreated);

    const result = await createFolder("parent-1", "Private");

    expect(result).toEqual(encryptedCreated);
    expect(nodesApi.updateNodeMetadata).toHaveBeenCalledWith("b", {
      isClientEncryptionEnabled: "true",
    });
    expect(
      useNodesStore.getState().contentByNodeId["parent-1"]?.nodes[0]?.metadata,
    ).toEqual({ isClientEncryptionEnabled: "true" });
  });

  it("inherits an ancestor client-side encryption policy", async () => {
    const vault = {
      ...makeNode("vault", "Vault"),
      parentId: null,
      metadata: { isClientEncryptionEnabled: "true" },
    };
    const parent = {
      ...makeNode("parent-1", "Nested"),
      parentId: "vault",
      metadata: {},
    };
    useNodesStore.setState({ ancestors: [vault], currentNode: parent });
    seedFolder("parent-1", []);
    const created = makeNode("b", "Private");
    const encryptedCreated = {
      ...created,
      metadata: { isClientEncryptionEnabled: "true" },
    };
    vi.mocked(nodesApi.createNode).mockResolvedValue(created);
    vi.mocked(nodesApi.updateNodeMetadata).mockResolvedValue(encryptedCreated);

    const result = await createFolder("parent-1", "Private");

    expect(result).toEqual(encryptedCreated);
    expect(nodesApi.updateNodeMetadata).toHaveBeenCalledWith("b", {
      isClientEncryptionEnabled: "true",
    });
  });

  it("does not patch metadata for folders created under plain parents", async () => {
    seedFolder("parent-1", []);
    const created = makeNode("b", "Public");
    vi.mocked(nodesApi.createNode).mockResolvedValue(created);

    await createFolder("parent-1", "Public");

    expect(nodesApi.updateNodeMetadata).not.toHaveBeenCalled();
  });

  it("sets an error message on API failure", async () => {
    seedFolder("parent-1", []);
    vi.mocked(nodesApi.createNode).mockRejectedValue(new Error("boom"));

    const result = await createFolder("parent-1", "Photos");

    expect(result).toBeNull();
    expect(useNodesStore.getState().error).toBe("Failed to create folder");
    expect(useNodesStore.getState().loading).toBe(false);
  });

  it("no-ops while a previous request is in flight", async () => {
    useNodesStore.setState({ loading: true });

    const result = await createFolder("parent-1", "Photos");

    expect(result).toBeNull();
    expect(nodesApi.createNode).not.toHaveBeenCalled();
  });
});

describe("deleteFolder", () => {
  it("calls deleteNode and removes the folder from cache", async () => {
    seedFolder("parent-1", [makeNode("a", "Drafts"), makeNode("b", "Photos")]);
    vi.mocked(nodesApi.deleteNode).mockResolvedValue();

    const ok = await deleteFolder("a", "parent-1");

    expect(ok).toBe(true);
    expect(nodesApi.deleteNode).toHaveBeenCalledWith("a", false);
    expect(
      useNodesStore
        .getState()
        .contentByNodeId["parent-1"]?.nodes.map((n) => n.id),
    ).toEqual(["b"]);
  });

  it("forwards skipTrash for permanent deletion", async () => {
    seedFolder("parent-1", [makeNode("a", "Drafts")]);
    vi.mocked(nodesApi.deleteNode).mockResolvedValue();

    await deleteFolder("a", "parent-1", true);

    expect(nodesApi.deleteNode).toHaveBeenCalledWith("a", true);
  });

  it("sets the error message on failure", async () => {
    seedFolder("parent-1", [makeNode("a", "Drafts")]);
    vi.mocked(nodesApi.deleteNode).mockRejectedValue(new Error("nope"));

    const ok = await deleteFolder("a", "parent-1");

    expect(ok).toBe(false);
    expect(useNodesStore.getState().error).toBe("Failed to delete folder");
  });

  it("works without a parentNodeId", async () => {
    vi.mocked(nodesApi.deleteNode).mockResolvedValue();

    const ok = await deleteFolder("a");

    expect(ok).toBe(true);
    expect(useNodesStore.getState().loading).toBe(false);
  });
});

describe("renameFolder", () => {
  it("returns false and skips the API call when the name is empty", async () => {
    const ok = await renameFolder("a", "  ", "parent-1");

    expect(ok).toBe(false);
    expect(nodesApi.renameNode).not.toHaveBeenCalled();
  });

  it("rejects duplicate names from cached siblings", async () => {
    seedFolder("parent-1", [makeNode("a", "Drafts"), makeNode("b", "Photos")]);

    const ok = await renameFolder("a", "Photos", "parent-1");

    expect(ok).toBe(false);
    expect(nodesApi.renameNode).not.toHaveBeenCalled();
    expect(useNodesStore.getState().error).toBe(
      "A folder with this name already exists",
    );
  });

  it("allows renaming a folder to its own current name", async () => {
    seedFolder("parent-1", [makeNode("a", "Drafts")]);
    const renamed = makeNode("a", "drafts");
    vi.mocked(nodesApi.renameNode).mockResolvedValue(renamed);

    const ok = await renameFolder("a", "drafts", "parent-1");

    expect(ok).toBe(true);
    expect(nodesApi.renameNode).toHaveBeenCalledWith("a", { name: "drafts" });
    expect(
      useNodesStore.getState().contentByNodeId["parent-1"]?.nodes[0]?.name,
    ).toBe("drafts");
  });

  it("sets the error message on failure", async () => {
    seedFolder("parent-1", [makeNode("a", "Drafts")]);
    vi.mocked(nodesApi.renameNode).mockRejectedValue(new Error("nope"));

    const ok = await renameFolder("a", "NewName", "parent-1");

    expect(ok).toBe(false);
    expect(useNodesStore.getState().error).toBe("Failed to rename folder");
  });
});
