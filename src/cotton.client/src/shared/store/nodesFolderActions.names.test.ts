import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { NodeDto } from "../api/layoutsApi";
import { nodesApi } from "../api/nodesApi";
import { createFolder as makeFolder } from "../../test/fileFixtures";
import { encryptFolderName } from "../crypto/folderDisplayMeta";
import { generateMasterKey } from "../crypto/keys";
import { useVault } from "../crypto/vault";
import { createFolder, renameFolder } from "./nodesFolderActions";
import { useNodesStore } from "./nodesStore";

const parent = makeFolder({ id: "parent", name: "Public" });

beforeEach(() => {
  useVault.getState().lock();
  useNodesStore.setState({
    currentNode: parent,
    ancestors: [],
    contentByNodeId: {},
    loading: false,
    error: null,
  });
});

afterEach(() => {
  useVault.getState().lock();
  vi.restoreAllMocks();
});

async function seedEncryptedSibling(): Promise<NodeDto> {
  useVault.getState().unlock(await generateMasterKey(), {
    persistToSession: false,
  });
  const sibling = makeFolder({
    id: "encrypted-sibling",
    parentId: parent.id,
    name: "opaque-name",
    metadata: { en: await encryptFolderName("Documents") },
  });
  vi.spyOn(nodesApi, "getChildren").mockResolvedValue({
    content: { ...parent, nodes: [sibling], files: [] },
    totalCount: 1,
  });
  return sibling;
}

describe("folder display-name conflicts under a plain parent", () => {
  it("rejects creation with an encrypted sibling's visible name", async () => {
    await seedEncryptedSibling();
    const create = vi
      .spyOn(nodesApi, "createNode")
      .mockResolvedValue(
        makeFolder({ parentId: parent.id, name: "documents" }),
      );

    expect(await createFolder(parent.id, "  documents  ")).toBeNull();

    expect(create).not.toHaveBeenCalled();
    expect(useNodesStore.getState()).toMatchObject({
      loading: false,
      error: "A folder with this name already exists",
    });
  });

  it("creates an available name without enabling encryption", async () => {
    await seedEncryptedSibling();
    const created = makeFolder({ parentId: parent.id, name: "Photos" });
    const create = vi.spyOn(nodesApi, "createNode").mockResolvedValue(created);

    expect(await createFolder(parent.id, "  Photos  ")).toEqual(created);

    expect(create).toHaveBeenCalledWith({
      parentId: parent.id,
      name: "Photos",
    });
  });

  it("rejects renaming a plain folder to an encrypted sibling's visible name", async () => {
    await seedEncryptedSibling();
    const source = makeFolder({
      id: "source",
      parentId: parent.id,
      name: "Photos",
    });
    vi.spyOn(nodesApi, "getNode").mockResolvedValue(source);
    const rename = vi.spyOn(nodesApi, "renameNode").mockResolvedValue(source);

    expect(await renameFolder(source.id, "documents", parent.id)).toBe(false);

    expect(rename).not.toHaveBeenCalled();
    expect(useNodesStore.getState()).toMatchObject({
      loading: false,
      error: "A folder with this name already exists",
    });
  });

  it("lets a plain folder keep its own name while the vault is unlocked", async () => {
    await seedEncryptedSibling();
    const source = makeFolder({
      id: "source",
      parentId: parent.id,
      name: "Photos",
    });
    vi.spyOn(nodesApi, "getNode").mockResolvedValue(source);
    vi.mocked(nodesApi.getChildren).mockResolvedValue({
      content: { ...parent, nodes: [source], files: [] },
      totalCount: 1,
    });
    const rename = vi.spyOn(nodesApi, "renameNode").mockResolvedValue(source);

    expect(await renameFolder(source.id, "Photos", parent.id)).toBe(true);

    expect(rename).toHaveBeenCalledWith(source.id, { name: "Photos" });
  });

  it("does not load siblings for ordinary creation while the vault is locked", async () => {
    const created = makeFolder({ parentId: parent.id, name: "Photos" });
    vi.spyOn(nodesApi, "createNode").mockResolvedValue(created);
    const get = vi.spyOn(nodesApi, "getChildren");

    expect(await createFolder(parent.id, "Photos")).toEqual(created);

    expect(get).not.toHaveBeenCalled();
  });
});
