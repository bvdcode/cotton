import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createFolder } from "../../test/fileFixtures";
import { nodesApi } from "../api/nodesApi";
import { generateMasterKey } from "./keys";
import { useVault } from "./vault";
import { encryptFolderName, readFolderName } from "./folderDisplayMeta";
import { createNamedFolder, prepareFolderName } from "./folderNameOperations";
import { FolderTransferNames } from "../move/FolderTransferNames";

describe("folder name mutations", () => {
  beforeEach(async () =>
    useVault
      .getState()
      .unlock(await generateMasterKey(), { persistToSession: false }),
  );
  afterEach(() => {
    useVault.getState().lock();
    vi.restoreAllMocks();
  });

  it("renames an encrypted folder without exposing the new name or replacing its stored name", async () => {
    const node = createFolder({
      name: "opaque-id",
      metadata: { en: await encryptFolderName("Old") },
    });
    const request = await prepareFolderName(node, " New ", true);
    expect(request.name).toBe(node.name);
    expect(JSON.stringify(request)).not.toContain("New");
    expect(
      await readFolderName({
        ...node,
        metadata: { en: request.metadata?.en ?? "" },
      }),
    ).toBe("New");
  });

  it("does not modify metadata when renaming a plain folder", async () => {
    expect(await prepareFolderName(createFolder(), " Report ", false)).toEqual({
      name: "Report",
    });
  });

  it("refuses encrypted creation when locked before any mutation request", async () => {
    const create = vi.spyOn(nodesApi, "createNode");
    useVault.getState().lock();
    await expect(createNamedFolder("parent", "Secret", true)).rejects.toThrow();
    expect(create).not.toHaveBeenCalled();
  });

  it("reuses one decoded destination listing and catches names added earlier in the batch", async () => {
    const parent = createFolder({ id: "parent" });
    const existing = createFolder({
      id: "existing",
      name: "opaque",
      metadata: { en: await encryptFolderName("Documents") },
    });
    const get = vi.spyOn(nodesApi, "getChildren").mockResolvedValue({
      content: { ...parent, nodes: [existing], files: [] },
      totalCount: 1,
    });
    const names = new FolderTransferNames(parent.id);
    await expect(names.check("documents")).rejects.toMatchObject({
      kind: "Folder",
    });
    await names.check("documents", existing.id);
    await names.check("Photos");
    await names.add("Photos", "new");
    await expect(names.check("PHOTOS")).rejects.toMatchObject({
      kind: "Folder",
    });
    expect(get).toHaveBeenCalledOnce();
  });
});
