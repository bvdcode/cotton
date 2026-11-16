import { afterEach, expect, it, vi } from "vitest";
import { createFile, createFolder } from "../../../test/fileFixtures";
import { nodesApi } from "../../../shared/api/nodesApi";
import { DroppedFolderResolver } from "./DroppedFolderResolver";
import {
  encryptFolderName,
  readFolderName,
} from "../../../shared/crypto/folderDisplayMeta";
import { generateMasterKey } from "../../../shared/crypto/keys";
import { useVault } from "../../../shared/crypto/vault";
import { useNodesStore } from "../../../shared/store/nodesStore";

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

it("inherits encryption through newly created folders without fetching their children", async () => {
  useVault.getState().unlock(await generateMasterKey(), {
    persistToSession: false,
  });
  vi.spyOn(useNodesStore.getState(), "addFolderToCache").mockImplementation(
    () => {},
  );
  const parent = createFolder({ id: "parent" });
  const get = vi.spyOn(nodesApi, "getChildren").mockResolvedValue({
    content: { node: parent, ...parent, nodes: [], files: [] },
    totalCount: 0,
  });
  const create = vi
    .spyOn(nodesApi, "createNode")
    .mockImplementation(async ({ parentId, name, metadata }) =>
      createFolder({ id: crypto.randomUUID(), parentId, name, metadata }),
    );
  const dropped = [
    { file: new File(["a"], "a.txt"), relativePath: "Photos/Day/a.txt" },
    { file: new File(["b"], "b.txt"), relativePath: "photos/day/b.txt" },
    { file: new File(["c"], "c.txt"), relativePath: "Photos/Night/c.txt" },
  ];
  const resolver = new DroppedFolderResolver(
    parent.id,
    "Files",
    true,
    () => false,
  );

  const grouped = await resolver.groupFiles(dropped, () => {});

  expect(get).toHaveBeenCalledOnce();
  expect(create).toHaveBeenCalledTimes(3);
  const names: string[] = [];
  for (const [request] of create.mock.calls) {
    expect(request.metadata?.isClientEncryptionEnabled).toBe("true");
    names.push(
      await readFolderName({ name: request.name, metadata: request.metadata }),
    );
    expect(request.name).not.toBe(names.at(-1));
  }
  expect(names[0]).toBe("Photos");
  expect(names.slice(1).sort()).toEqual(["Day", "Night"]);
  for (const nodeId of grouped.keys()) {
    expect(resolver.decideEncryption(nodeId)).toEqual({
      encrypt: true,
      vaultLocked: false,
    });
  }
  expect([...grouped.values()]).toEqual([
    {
      label: "Files / Photos / Day",
      files: [dropped[0].file, dropped[1].file],
    },
    { label: "Files / Photos / Night", files: [dropped[2].file] },
  ]);
});
