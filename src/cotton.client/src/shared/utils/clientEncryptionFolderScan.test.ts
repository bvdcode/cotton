import { afterEach, describe, expect, it, vi } from "vitest";
import * as nodeChildren from "../api/nodeChildren";
import type { NodeResponse } from "../api/nodesApi";
import { createFile, createFolder } from "../../test/fileFixtures";
import {
  collectEncryptedFilesInFoldersForClientEncryption,
  collectPlainFilesInFoldersForClientEncryption,
} from "./clientEncryptionFolderScan";

const page = (
  id: string,
  files: NodeResponse["content"]["files"] = [],
  nodes: NodeResponse["content"]["nodes"] = [],
): NodeResponse => ({
  content: {
    node: createFolder({ id }),
    ...createFolder({ id }),
    files,
    nodes,
  },
  totalCount: files.length + nodes.length,
});

const vault = createFolder({
  id: "vault",
  metadata: { isClientEncryptionEnabled: "true" },
});
const encryptedFile = (id: string, nodeId: string) =>
  createFile({
    id,
    nodeId,
    metadata: { isClientEncrypted: "true" },
  });

afterEach(() => vi.restoreAllMocks());

describe("client encryption folder scan", () => {
  it("excludes protected subtrees but collects leftover encrypted files in ordinary folders", async () => {
    const direct = encryptedFile("direct", "home");
    const nested = encryptedFile("nested", "ordinary");
    const get = vi
      .spyOn(nodeChildren, "fetchNodeChildren")
      .mockImplementation(async (id) => {
        switch (id) {
          case "home":
            return page(
              id,
              [direct, createFile()],
              [vault, createFolder({ id: "ordinary" })],
            );
          case "ordinary":
            return page(
              id,
              [nested],
              [createFolder({ ...vault, id: "nested-vault" })],
            );
          default:
            return page(id, [encryptedFile("protected", id)]);
        }
      });

    const result = await collectEncryptedFilesInFoldersForClientEncryption([
      "home",
    ]);

    expect(result.files).toEqual([direct, nested]);
    expect(result.folders.map((folder) => folder.id)).toEqual(["ordinary"]);
    expect(result.scannedFolders).toBe(2);
    expect(result.truncated).toBe(false);
    expect(get.mock.calls.map(([id]) => id)).toEqual(["home", "ordinary"]);
  });

  it("does not consume the folder limit on excluded vaults", async () => {
    const get = vi
      .spyOn(nodeChildren, "fetchNodeChildren")
      .mockResolvedValue(
        page(
          "home",
          [],
          [vault, createFolder({ ...vault, id: "other-vault" })],
        ),
      );

    const result = await collectEncryptedFilesInFoldersForClientEncryption(
      ["home"],
      { maxFolders: 2 },
    );

    expect(result.files).toEqual([]);
    expect(result.folders).toEqual([]);
    expect(result.truncated).toBe(false);
    expect(get).toHaveBeenCalledOnce();
  });

  it("continues pagination after an excluded folder", async () => {
    const leftover = encryptedFile("leftover", "home");
    const get = vi
      .spyOn(nodeChildren, "fetchNodeChildren")
      .mockImplementation(async (id) => {
        throw new Error(`Unexpected folder read: ${id}`);
      })
      .mockResolvedValueOnce({ ...page("home", [], [vault]), totalCount: 2 })
      .mockResolvedValueOnce({ ...page("home", [leftover]), totalCount: 2 });

    const result = await collectEncryptedFilesInFoldersForClientEncryption([
      "home",
    ]);

    expect(result.files).toEqual([leftover]);
    expect(result.truncated).toBe(false);
    expect(get.mock.calls.map(([id, options]) => [id, options?.page])).toEqual([
      ["home", 1],
      ["home", 2],
    ]);
  });

  it("keeps encrypted child folders in encryption scans", async () => {
    const plain = createFile({ nodeId: "vault" });
    vi.spyOn(nodeChildren, "fetchNodeChildren").mockImplementation(
      async (id) => {
        if (id === "home") {
          return page(id, [], [vault]);
        }
        return page(id, [plain, encryptedFile("encrypted", id)]);
      },
    );

    const encryption = await collectPlainFilesInFoldersForClientEncryption([
      "home",
    ]);
    expect(encryption.files).toEqual([plain]);
    expect(encryption.folders).toEqual([vault]);
  });
});
