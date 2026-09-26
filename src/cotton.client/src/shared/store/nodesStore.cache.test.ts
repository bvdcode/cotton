import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("./nodesActionInternals", () => ({
  resetNodesActionsInternals: vi.fn(),
}));

import type { NodeDto } from "../api/layoutsApi";
import type { NodeContentDto, NodeFileManifestDto } from "../api/nodesApi";
import { NODES_STORAGE_KEY } from "../config/storageKeys";
import {
  DISPLAY_META_KEY,
  ENCRYPTED_FLAG_KEY,
  encryptDisplayMeta,
  generateMasterKey,
  useVault,
} from "../crypto";
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
  sessionStorage.clear();
  resetStore();
  vi.clearAllMocks();
});

afterEach(() => {
  resetStore();
  useVault.getState().lock();
  sessionStorage.clear();
});

describe("upsertFileInCache", () => {
  it("appends uploaded files to cached parent content", () => {
    seedParent("parent-1", [], [makeFile("f1", "old.txt")]);
    const uploaded = makeFile("f2", "new.txt");

    const updated = useNodesStore
      .getState()
      .upsertFileInCache("parent-1", uploaded);

    expect(updated).toBe(true);
    expect(
      useNodesStore
        .getState()
        .contentByNodeId["parent-1"]?.files.map((f) => f.id),
    ).toEqual(["f1", "f2"]);
    expect(useNodesStore.getState().lastUpdatedByNodeId["parent-1"]).toEqual(
      expect.any(Number),
    );
  });

  it("replaces existing uploaded files in cached parent content", () => {
    seedParent("parent-1", [], [makeFile("f1", "old.txt")]);
    const uploaded = makeFile("f1", "fresh.txt", {
      contentType: "image/jpeg",
    });

    const updated = useNodesStore
      .getState()
      .upsertFileInCache("parent-1", uploaded);

    expect(updated).toBe(true);
    expect(useNodesStore.getState().contentByNodeId["parent-1"]?.files).toEqual(
      [uploaded],
    );
  });

  it("keeps cache unchanged when the parent content is not cached", () => {
    const before = useNodesStore.getState().contentByNodeId;

    const updated = useNodesStore
      .getState()
      .upsertFileInCache("nowhere", makeFile("f1", "new.txt"));

    expect(updated).toBe(false);
    expect(useNodesStore.getState().contentByNodeId).toBe(before);
  });
});

describe("optimisticSetFilePreviewHash", () => {
  it("updates only the matching file", () => {
    seedParent(
      "parent-1",
      [],
      [makeFile("f1", "a.txt", { previewHashEncryptedHex: "old" })],
    );

    useNodesStore
      .getState()
      .optimisticSetFilePreviewHash("parent-1", "f1", "new-hash");

    expect(
      useNodesStore.getState().contentByNodeId["parent-1"]?.files[0]
        ?.previewHashEncryptedHex,
    ).toBe("new-hash");
  });

  it("keeps the same content object when the hash is already current", () => {
    seedParent(
      "parent-1",
      [],
      [makeFile("f1", "a.txt", { previewHashEncryptedHex: "stable" })],
    );
    const before = useNodesStore.getState().contentByNodeId["parent-1"];

    const updated = useNodesStore
      .getState()
      .optimisticSetFilePreviewHash("parent-1", "f1", "stable");

    expect(updated).toBe(false);
    expect(useNodesStore.getState().contentByNodeId["parent-1"]).toBe(before);
  });

  it("reports whether a matching file was updated", () => {
    seedParent(
      "parent-1",
      [],
      [makeFile("f1", "a.txt", { previewHashEncryptedHex: "old" })],
    );

    const updated = useNodesStore
      .getState()
      .optimisticSetFilePreviewHash("parent-1", "f1", "new-hash");
    const missing = useNodesStore
      .getState()
      .optimisticSetFilePreviewHash("parent-1", "missing", "new-hash");

    expect(updated).toBe(true);
    expect(missing).toBe(false);
  });
});

describe("optimisticDeleteFile", () => {
  it("removes the file from the parent's files list", () => {
    seedParent(
      "parent-1",
      [],
      [makeFile("f1", "a.txt"), makeFile("f2", "b.txt")],
    );

    useNodesStore.getState().optimisticDeleteFile("parent-1", "f1");

    expect(
      useNodesStore
        .getState()
        .contentByNodeId["parent-1"]?.files.map((f) => f.id),
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

describe("refreshCachedFileDisplayMetadata", () => {
  it("updates cached encrypted file names after the vault is unlocked", async () => {
    useVault.getState().unlock(await generateMasterKey());
    const encryptedMeta = await encryptDisplayMeta({
      name: "private.pdf",
      contentType: "application/pdf",
    });
    seedParent(
      "parent-1",
      [],
      [
        makeFile("f1", "11111111-2222-4333-8444-555555555555", {
          contentType: "application/octet-stream",
          metadata: {
            [ENCRYPTED_FLAG_KEY]: "true",
            [DISPLAY_META_KEY]: encryptedMeta,
          },
        }),
      ],
    );

    await useNodesStore.getState().refreshCachedFileDisplayMetadata();

    expect(
      useNodesStore.getState().contentByNodeId["parent-1"]?.files[0],
    ).toMatchObject({
      name: "private.pdf",
      contentType: "application/pdf",
    });
  });

  it("does not persist decrypted encrypted file display metadata", async () => {
    useVault.getState().unlock(await generateMasterKey());
    const encryptedMeta = await encryptDisplayMeta({
      name: "private.pdf",
      contentType: "application/pdf",
    });
    const parent = makeNode("parent-1", "Parent");
    useNodesStore.setState({
      currentNode: parent,
      rootNodeId: parent.id,
    });
    seedParent(
      "parent-1",
      [],
      [
        makeFile("f1", "11111111-2222-4333-8444-555555555555", {
          contentType: "application/octet-stream",
          metadata: {
            [ENCRYPTED_FLAG_KEY]: "true",
            [DISPLAY_META_KEY]: encryptedMeta,
          },
        }),
      ],
    );

    await useNodesStore.getState().refreshCachedFileDisplayMetadata();

    const raw = sessionStorage.getItem(NODES_STORAGE_KEY) ?? "";
    expect(
      useNodesStore.getState().contentByNodeId["parent-1"]?.files[0],
    ).toMatchObject({
      name: "private.pdf",
      contentType: "application/pdf",
    });
    expect(raw).toContain("11111111-2222-4333-8444-555555555555");
    expect(raw).toContain("application/octet-stream");
    expect(raw).not.toContain("private.pdf");
    expect(raw).not.toContain("application/pdf");
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
