import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("../api/nodesApi", () => ({
  nodesApi: {
    createNode: vi.fn(),
    deleteNode: vi.fn(),
    renameNode: vi.fn(),
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
