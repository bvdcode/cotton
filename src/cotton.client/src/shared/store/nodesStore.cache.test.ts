import {
  seedFolderListing,
  seedNodeNavigation,
  getFolderListing,
  getCurrentNode,
  getCurrentAncestors,
} from "../../test/nodeCacheFixtures";
import { createFolder } from "../../test/fileFixtures";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import type { NodeDto } from "../api/layoutsApi";
import type { NodeContentDto, NodeFileManifestDto } from "../api/nodesApi";
import { queryClient } from "../api/queries/queryClient";
import { queryKeys } from "../api/queries/queryKeys";
import {
  DISPLAY_META_KEY,
  ENCRYPTED_FLAG_KEY,
  encryptDisplayMeta,
  generateMasterKey,
  useVault,
} from "../crypto";
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
    node: createFolder({ id: parentId }),
    id: parentId,
    createdAt: "",
    updatedAt: "",
    nodes,
    files,
  };
  seedFolderListing(parentId, content);
};

const resetStore = () => {
  useNodesStore.getState().reset("user-1");
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
    expect(getFolderListing("parent-1")?.files.map((f) => f.id)).toEqual([
      "f1",
      "f2",
    ]);
    expect(
      queryClient.getQueryState(
        queryKeys.nodeChildren.complete("parent-1", "user-1"),
      )?.isInvalidated,
    ).toBe(true);
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
    expect(getFolderListing("parent-1")?.files).toEqual([uploaded]);
  });

  it("keeps cache unchanged when the parent content is not cached", () => {
    const before = getFolderListing("nowhere");

    const updated = useNodesStore
      .getState()
      .upsertFileInCache("nowhere", makeFile("f1", "new.txt"));

    expect(updated).toBe(false);
    expect(getFolderListing("nowhere")).toBe(before);
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
      getFolderListing("parent-1")?.files[0]?.previewHashEncryptedHex,
    ).toBe("new-hash");
  });

  it("keeps the same content object when the hash is already current", () => {
    seedParent(
      "parent-1",
      [],
      [makeFile("f1", "a.txt", { previewHashEncryptedHex: "stable" })],
    );
    const before = getFolderListing("parent-1");

    const updated = useNodesStore
      .getState()
      .optimisticSetFilePreviewHash("parent-1", "f1", "stable");

    expect(updated).toBe(false);
    expect(getFolderListing("parent-1")).toBe(before);
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

    expect(getFolderListing("parent-1")?.files.map((f) => f.id)).toEqual([
      "f2",
    ]);
  });

  it("is a no-op for missing files and parents", () => {
    seedParent("parent-1", [], [makeFile("f1", "a.txt")]);

    useNodesStore.getState().optimisticDeleteFile("parent-1", "missing");
    expect(getFolderListing("parent-1")?.files.length).toBe(1);

    useNodesStore.getState().optimisticDeleteFile("nowhere", "anything");
    expect(getFolderListing("nowhere")).toBeUndefined();
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

    expect(getFolderListing("parent-1")?.files[0]).toMatchObject({
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
    const persistedEntries = Object.entries(sessionStorage);
    const parent = makeNode("parent-1", "Parent");
    useNodesStore.setState({ rootNodeId: parent.id });
    seedNodeNavigation(parent, []);

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

    expect(getFolderListing("parent-1")?.files[0]).toMatchObject({
      name: "private.pdf",
      contentType: "application/pdf",
    });
    expect(Object.entries(sessionStorage)).toEqual(persistedEntries);
  });
});

describe("reset", () => {
  it("wipes per-user cache and accepts a new owner", () => {
    useNodesStore.setState({
      cacheOwnerUserId: "user-1",
      rootNodeId: "root",
      loading: true,
      error: "boom",
    });
    seedNodeNavigation(makeNode("a", "Drafts"), [makeNode("p", "Parent")]);
    seedFolderListing("a", {
      node: createFolder({ id: "a" }),
      id: "a",
      createdAt: "",
      updatedAt: "",
      nodes: [],
      files: [],
    });

    useNodesStore.getState().reset("user-2");

    const state = useNodesStore.getState();
    expect(state.cacheOwnerUserId).toBe("user-2");
    expect(state.rootNodeId).toBeNull();
    expect(getCurrentNode()).toBeNull();
    expect(getCurrentAncestors()).toEqual([]);
    expect(queryClient.getQueriesData({ queryKey: ["nodeChildren"] })).toEqual(
      [],
    );
    expect(
      queryClient.getQueriesData({ queryKey: ["nodeNavigation"] }),
    ).toEqual([]);
    expect(state.loading).toBe(false);
    expect(state.error).toBeNull();
  });

  it("clears the owner on logout", () => {
    useNodesStore.getState().reset("user-1");
    useNodesStore.getState().reset(null);
    expect(useNodesStore.getState().cacheOwnerUserId).toBeNull();
  });

  it("keeps the previous owner when called without an argument", () => {
    useNodesStore.setState({ cacheOwnerUserId: "user-1" });

    useNodesStore.getState().reset();

    expect(useNodesStore.getState().cacheOwnerUserId).toBe("user-1");
  });
});
