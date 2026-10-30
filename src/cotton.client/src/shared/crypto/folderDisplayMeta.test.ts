import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createFolder } from "../../test/fileFixtures";
import { generateMasterKey } from "./keys";
import { useVault } from "./vault";
import {
  encryptFolderName,
  readFolderName,
  applyDisplayMetaToNode,
  applyDisplayMetaToNodes,
} from "./folderDisplayMeta";
import { projectSearchResult } from "./searchDisplayMeta";

describe("encrypted folder names", () => {
  beforeEach(async () => useVault.getState().unlock(await generateMasterKey()));
  afterEach(() => {
    useVault.getState().lock();
    vi.restoreAllMocks();
  });

  it("keeps existing plain names inside encryption-enabled folders", async () => {
    const node = createFolder({
      name: "Documents",
      metadata: { isClientEncryptionEnabled: "true" },
    });
    expect(await applyDisplayMetaToNode(node)).toBe(node);
    expect(await readFolderName(node)).toBe("Documents");
  });

  it("encrypts only the display payload and preserves raw input", async () => {
    const en = await encryptFolderName(" Документы ");
    const node = createFolder({ name: "opaque", metadata: { en } });
    expect(en).not.toContain("Документы");
    expect(await applyDisplayMetaToNode(node)).toEqual({
      ...node,
      name: "Документы",
    });
    expect(node.name).toBe("opaque");
    expect(JSON.stringify(node)).not.toContain("Документы");
    expect(await encryptFolderName("Документы")).not.toBe(en);
  });

  it("discards decoded names after locking and does not reuse them with another key", async () => {
    const node = createFolder({
      name: "opaque",
      metadata: { en: await encryptFolderName("Secret") },
    });
    expect((await applyDisplayMetaToNode(node)).name).toBe("Secret");
    useVault.getState().lock();
    expect(await applyDisplayMetaToNode(node)).toBe(node);
    useVault.getState().unlock(await generateMasterKey());
    expect((await applyDisplayMetaToNode(node)).name).toBe("opaque");
  });

  it("decodes an unchanged node only once for incremental pages", async () => {
    const nodes = await Promise.all(
      Array.from({ length: 32 }, async (_, i) =>
        createFolder({
          id: String(i),
          name: `opaque-${i}`,
          metadata: { en: await encryptFolderName(`Folder ${i}`) },
        }),
      ),
    );
    const decrypt = vi.spyOn(crypto.subtle, "decrypt");
    await applyDisplayMetaToNodes(nodes.slice(0, 16));
    await applyDisplayMetaToNodes(nodes);
    expect(decrypt).toHaveBeenCalledTimes(nodes.length);
  });

  it("builds search paths from ancestor metadata without modifying server results", async () => {
    const root = createFolder({ id: "root", parentId: null, name: "Default" });
    const parent = createFolder({
      id: "parent",
      parentId: root.id,
      name: "opaque",
      metadata: { en: await encryptFolderName("Documents") },
    });
    const child = createFolder({
      id: "child",
      parentId: parent.id,
      name: "Report",
    });
    const source = {
      nodes: [child],
      files: [],
      ancestors: [root, parent],
      nodePaths: { child: "/Default/opaque/Report" },
      filePaths: {},
    };
    expect((await projectSearchResult(source))?.nodePaths.child).toBe(
      "/Default/Documents/Report",
    );
    expect(source.nodePaths.child).toBe("/Default/opaque/Report");
  });
});
