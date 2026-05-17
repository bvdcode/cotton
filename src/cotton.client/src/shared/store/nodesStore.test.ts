import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("./nodesActionInternals", () => ({
  resetNodesActionsInternals: vi.fn(),
}));

import type { NodeDto } from "../api/layoutsApi";
import type { NodeContentDto, NodeFileManifestDto } from "../api/nodesApi";
import { resetNodesActionsInternals } from "./nodesActionInternals";
import { useNodesStore } from "./nodesStore";

const makeNode = (id: string, name: string): NodeDto => ({
  id,
  createdAt: "",
  updatedAt: "",
  layoutId: "layout-1",
  parentId: "parent-1",
  name,
  metadata: {},
});

const makeFile = (
  id: string,
  name: string,
  overrides: Partial<NodeFileManifestDto> = {},
): NodeFileManifestDto => ({
  id,
  createdAt: "",
  updatedAt: "",
  nodeId: "parent-1",
  ownerId: "user-1",
  name,
  contentType: "text/plain",
  sizeBytes: 0,
  metadata: {},
  ...overrides,
});

const seedParent = (
  parentId: string,
  nodes: NodeDto[],
  files: NodeFileManifestDto[],
) => {
  const content: NodeContentDto = {
    id: parentId,
    createdAt: "",
    updatedAt: "",
    nodes,
    files,
  };
  useNodesStore.setState((prev) => ({
    ...prev,
    contentByNodeId: { ...prev.contentByNodeId, [parentId]: content },
  }));
};

const resetStore = () => {
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
});

afterEach(() => {
  resetStore();
});

describe("addFolderToCache", () => {
  it("appends a new folder to the parent's nodes", () => {
    seedParent("parent-1", [makeNode("a", "Drafts")], []);

    useNodesStore.getState().addFolderToCache("parent-1", makeNode("b", "Photos"));

    expect(
      useNodesStore.getState().contentByNodeId["parent-1"]?.nodes.map((n) => n.id),
    ).toEqual(["a", "b"]);
  });

  it("is idempotent when the folder is already present", () => {
    seedParent("parent-1", [makeNode("a", "Drafts")], []);

    useNodesStore.getState().addFolderToCache("parent-1", makeNode("a", "Drafts"));

    expect(
      useNodesStore.getState().contentByNodeId["parent-1"]?.nodes.length,
    ).toBe(1);
  });

  it("is a no-op when the parent has no cached content", () => {
    useNodesStore.getState().addFolderToCache("nowhere", makeNode("a", "Drafts"));

    expect(useNodesStore.getState().contentByNodeId["nowhere"]).toBeUndefined();
  });
});

describe("optimisticRenameFile", () => {
  it("renames the matching file", () => {
    seedParent("parent-1", [], [makeFile("f1", "old.txt")]);

    useNodesStore.getState().optimisticRenameFile("parent-1", "f1", "new.txt");

    expect(
      useNodesStore.getState().contentByNodeId["parent-1"]?.files[0]?.name,
    ).toBe("new.txt");
  });

  it("leaves other files alone", () => {
    seedParent("parent-1", [], [
      makeFile("f1", "a.txt"),
      makeFile("f2", "b.txt"),
    ]);

    useNodesStore.getState().optimisticRenameFile("parent-1", "f2", "renamed.txt");

    expect(
      useNodesStore.getState().contentByNodeId["parent-1"]?.files.map((f) => f.name),
    ).toEqual(["a.txt", "renamed.txt"]);
  });
});

describe("optimisticSetFilePreviewHash", () => {
  it("updates only the matching file", () => {
    seedParent("parent-1", [], [
      makeFile("f1", "a.txt", { previewHashEncryptedHex: "old" }),
    ]);

    useNodesStore
      .getState()
      .optimisticSetFilePreviewHash("parent-1", "f1", "new-hash");

    expect(
      useNodesStore.getState().contentByNodeId["parent-1"]?.files[0]
        ?.previewHashEncryptedHex,
    ).toBe("new-hash");
  });

  it("keeps the same content object when the hash is already current", () => {
    seedParent("parent-1", [], [
      makeFile("f1", "a.txt", { previewHashEncryptedHex: "stable" }),
    ]);
    const before = useNodesStore.getState().contentByNodeId["parent-1"];

    useNodesStore
      .getState()
      .optimisticSetFilePreviewHash("parent-1", "f1", "stable");

    expect(useNodesStore.getState().contentByNodeId["parent-1"]).toBe(before);
  });
});

describe("optimisticDeleteFile", () => {
  it("removes the file from the parent's files list", () => {
    seedParent("parent-1", [], [
      makeFile("f1", "a.txt"),
      makeFile("f2", "b.txt"),
    ]);

    useNodesStore.getState().optimisticDeleteFile("parent-1", "f1");

    expect(
      useNodesStore.getState().contentByNodeId["parent-1"]?.files.map((f) => f.id),
    ).toEqual(["f2"]);
  });

  it("is a no-op for missing files and parents", () => {
    seedParent("parent-1", [], [makeFile("f1", "a.txt")]);

    useNodesStore.getState().optimisticDeleteFile("parent-1", "missing");
    expect(
      useNodesStore.getState().contentByNodeId["parent-1"]?.files.length,
    ).toBe(1);

    useNodesStore.getState().optimisticDeleteFile("nowhere", "anything");
    expect(useNodesStore.getState().contentByNodeId["nowhere"]).toBeUndefined();
  });
});

describe("reset", () => {
  it("wipes per-user cache and accepts a new owner", () => {
    useNodesStore.setState({
      cacheOwnerUserId: "user-1",
      rootNodeId: "root",
      currentNode: makeNode("a", "Drafts"),
      ancestors: [makeNode("p", "Parent")],
      contentByNodeId: {
        a: { id: "a", createdAt: "", updatedAt: "", nodes: [], files: [] },
      },
      loading: true,
      error: "boom",
    });

    useNodesStore.getState().reset("user-2");

    const state = useNodesStore.getState();
    expect(resetNodesActionsInternals).toHaveBeenCalledOnce();
    expect(state.cacheOwnerUserId).toBe("user-2");
    expect(state.rootNodeId).toBeNull();
    expect(state.currentNode).toBeNull();
    expect(state.ancestors).toEqual([]);
    expect(state.contentByNodeId).toEqual({});
    expect(state.loading).toBe(false);
    expect(state.error).toBeNull();
  });

  it("keeps the previous owner when called without an argument", () => {
    useNodesStore.setState({ cacheOwnerUserId: "user-1" });

    useNodesStore.getState().reset();

    expect(useNodesStore.getState().cacheOwnerUserId).toBe("user-1");
  });
});
