import { afterEach, expect, it, vi } from "vitest";
import { createFile, createFolder } from "../../../test/fileFixtures";
import { nodesApi } from "../../../shared/api/nodesApi";
import { DroppedFolderResolver } from "./DroppedFolderResolver";
import { encryptFolderName } from "../../../shared/crypto/folderDisplayMeta";
import { generateMasterKey } from "../../../shared/crypto/keys";
import { useVault } from "../../../shared/crypto/vault";

afterEach(() => {
  useVault.getState().lock();
  vi.restoreAllMocks();
});

it("merges dropped folders by name key and preserves the existing name", async () => {
  const parent = createFolder({ id: "parent" });
  const existing = createFolder({
    id: "existing",
    name: "Photos",
    parentId: parent.id,
  });
  const lookup = vi
    .spyOn(nodesApi, "lookupSiblingNames")
    .mockResolvedValue({ nodes: [existing], files: [], takenNameKeys: [] });
  const create = vi.spyOn(nodesApi, "createNode");
  const file = new File(["photo"], "photo.jpg");
  const secondFile = new File(["other"], "other.jpg");
  const resolver = new DroppedFolderResolver(
    parent.id,
    "Files",
    false,
    () => false,
  );

  const grouped = await resolver.groupFiles(
    [
      { file, relativePath: "photos/photo.jpg" },
      { file: secondFile, relativePath: "PHOTOS/other.jpg" },
    ],
    () => {},
  );

  expect(grouped.get(existing.id)).toEqual({
    label: "Files / Photos",
    files: [file, secondFile],
  });
  expect(lookup).toHaveBeenCalledOnce();
  expect(lookup).toHaveBeenCalledWith(parent.id, ["photos", "PHOTOS"], true);
  expect(create).not.toHaveBeenCalled();
});

it("reuses a renamed dropped folder when a file occupies its original name", async () => {
  const parent = createFolder({ id: "parent" });
  const fileConflict = createFile({ id: "conflict", name: "Photos" });
  const created = createFolder({
    id: "created",
    name: "Photos (folder 2)",
    parentId: parent.id,
  });
  const lookup = vi
    .spyOn(nodesApi, "lookupSiblingNames")
    .mockResolvedValueOnce({
      nodes: [],
      files: [fileConflict],
      takenNameKeys: [],
    })
    .mockResolvedValueOnce({
      nodes: [],
      files: [fileConflict],
      takenNameKeys: ["photos", "photos (folder)"],
    });
  const create = vi.spyOn(nodesApi, "createNode").mockResolvedValue(created);
  const first = new File(["one"], "one.jpg");
  const second = new File(["two"], "two.jpg");
  const resolver = new DroppedFolderResolver(
    parent.id,
    "Files",
    false,
    () => false,
  );

  const grouped = await resolver.groupFiles(
    [
      { file: first, relativePath: "Photos/one.jpg" },
      { file: second, relativePath: "photos/two.jpg" },
    ],
    () => {},
  );

  expect(grouped.get(created.id)).toEqual({
    label: "Files / Photos (folder 2)",
    files: [first, second],
  });
  expect(lookup).toHaveBeenCalledTimes(2);
  expect(create).toHaveBeenCalledOnce();
  expect(create).toHaveBeenCalledWith({
    parentId: parent.id,
    name: "Photos (folder 2)",
  });
});

it("reuses an encrypted folder's visible name under a plain parent", async () => {
  useVault.getState().unlock(await generateMasterKey(), {
    persistToSession: false,
  });
  const parent = createFolder({ id: "parent" });
  const existing = createFolder({
    id: "existing",
    name: "opaque-name",
    parentId: parent.id,
    metadata: {
      en: await encryptFolderName("Photos"),
      isClientEncryptionEnabled: "true",
    },
  });
  const get = vi.spyOn(nodesApi, "getChildren").mockResolvedValue({
    content: {
      node: parent,
      ...parent,
      nodes: [existing],
      files: [],
    },
    totalCount: 1,
  });
  const create = vi
    .spyOn(nodesApi, "createNode")
    .mockResolvedValue(createFolder({ id: "new", name: "photos" }));
  vi.spyOn(nodesApi, "lookupSiblingNames").mockResolvedValue({
    nodes: [],
    files: [],
    takenNameKeys: [],
  });
  const file = new File(["photo"], "photo.jpg");
  const secondFile = new File(["other"], "other.jpg");
  const resolver = new DroppedFolderResolver(
    parent.id,
    "Files",
    false,
    () => false,
  );

  const grouped = await resolver.groupFiles(
    [
      { file, relativePath: "photos/photo.jpg" },
      { file: secondFile, relativePath: "PHOTOS/other.jpg" },
    ],
    () => {},
  );

  expect(grouped.get(existing.id)).toEqual({
    label: "Files / Photos",
    files: [file, secondFile],
  });
  expect(resolver.decideEncryption(existing.id)).toEqual({
    encrypt: true,
    vaultLocked: false,
  });
  expect(get).toHaveBeenCalledOnce();
  expect(create).not.toHaveBeenCalled();
});
