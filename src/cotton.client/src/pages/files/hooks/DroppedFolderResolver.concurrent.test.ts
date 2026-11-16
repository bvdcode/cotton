import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { nodesApi } from "../../../shared/api/nodesApi";
import { useNodesStore } from "../../../shared/store/nodesStore";
import { uploadConfig } from "../../../shared/upload/config";
import { createFile, createFolder } from "../../../test/fileFixtures";
import { DroppedFolderResolver } from "./DroppedFolderResolver";

beforeEach(() => {
  vi.spyOn(useNodesStore.getState(), "addFolderToCache").mockImplementation(
    () => {},
  );
});

afterEach(() => {
  vi.restoreAllMocks();
});

it("bounds concurrent creation and preserves input order despite out-of-order responses", async () => {
  const release = vi.fn<() => void>();
  const gate = new Promise<void>((resolve) => {
    release.mockImplementation(resolve);
  });
  const lookup = vi.spyOn(nodesApi, "lookupSiblingNames").mockResolvedValue({
    nodes: [],
    files: [],
    takenNameKeys: [],
  });
  let active = 0;
  let peak = 0;
  const create = vi
    .spyOn(nodesApi, "createNode")
    .mockImplementation(async ({ parentId, name }) => {
      active += 1;
      peak = Math.max(peak, active);
      if (name === "Folder-0") {
        await gate;
      } else {
        await Promise.resolve();
      }
      active -= 1;
      return createFolder({ id: name, parentId, name });
    });
  const dropped = Array.from({ length: 20 }, (_, index) => ({
    file: new File(["data"], `file-${index}.txt`),
    relativePath: `Folder-${index}/file-${index}.txt`,
  }));
  const progress: number[] = [];
  const resolver = new DroppedFolderResolver(
    "root",
    "Files",
    false,
    () => false,
  );
  const pending = resolver.groupFiles(dropped, (count) => progress.push(count));

  await vi.waitFor(() => {
    expect(create).toHaveBeenCalledTimes(dropped.length);
  });
  expect(progress.at(-1)).toBe(dropped.length - 1);
  release();
  const grouped = await pending;

  expect(peak).toBeGreaterThan(1);
  expect(peak).toBeLessThanOrEqual(uploadConfig.maxConcurrentFolderCreations);
  expect(lookup).toHaveBeenCalledOnce();
  expect([...grouped.keys()]).toEqual(
    dropped.map((_, index) => `Folder-${index}`),
  );
  expect([...grouped.values()].flatMap((bucket) => bucket.files)).toEqual(
    dropped.map((item) => item.file),
  );
  expect(progress).toEqual(
    Array.from({ length: dropped.length + 1 }, (_, i) => i),
  );
});

it("shares pending ancestors and treats newly created folders as empty", async () => {
  const lookup = vi.spyOn(nodesApi, "lookupSiblingNames").mockResolvedValue({
    nodes: [],
    files: [],
    takenNameKeys: [],
  });
  const createdIds = new Set(["root"]);
  const create = vi
    .spyOn(nodesApi, "createNode")
    .mockImplementation(async ({ parentId, name }) => {
      expect(createdIds.has(parentId)).toBe(true);
      await Promise.resolve();
      const id = `${parentId}/${name}`;
      expect(createdIds.has(id)).toBe(false);
      createdIds.add(id);
      return createFolder({ id, parentId, name });
    });
  const dropped = [
    { file: new File(["a"], "a.txt"), relativePath: "Photos/Day/a.txt" },
    { file: new File(["b"], "b.txt"), relativePath: "photos/day/b.txt" },
    { file: new File(["c"], "c.txt"), relativePath: "Photos/Night/c.txt" },
    { file: new File(["d"], "d.txt"), relativePath: "PHOTOS/DAY/d.txt" },
  ];

  const grouped = await new DroppedFolderResolver(
    "root",
    "Files",
    false,
    () => false,
  ).groupFiles(dropped, () => {});

  expect(create).toHaveBeenCalledTimes(3);
  expect(lookup).toHaveBeenCalledOnce();
  expect(grouped.get("root/Photos/Day")).toEqual({
    label: "Files / Photos / Day",
    files: [dropped[0].file, dropped[1].file, dropped[3].file],
  });
  expect(grouped.get("root/Photos/Night")?.files).toEqual([dropped[2].file]);
});

it("shares a pending renamed folder with an explicitly dropped alias", async () => {
  vi.spyOn(nodesApi, "lookupSiblingNames").mockResolvedValue({
    nodes: [],
    files: [createFile({ name: "Photos" })],
    takenNameKeys: ["photos"],
  });
  const create = vi
    .spyOn(nodesApi, "createNode")
    .mockImplementation(async ({ parentId, name }) =>
      createFolder({ id: name, parentId, name }),
    );
  const dropped = [
    { file: new File(["a"], "a.txt"), relativePath: "Photos/a.txt" },
    { file: new File(["b"], "b.txt"), relativePath: "Photos (folder)/b.txt" },
  ];

  const grouped = await new DroppedFolderResolver(
    "root",
    "Files",
    false,
    () => false,
  ).groupFiles(dropped, () => {});

  expect(create).toHaveBeenCalledOnce();
  expect(create).toHaveBeenCalledWith({
    parentId: "root",
    name: "Photos (folder)",
  });
  expect([...grouped.values()]).toEqual([
    {
      label: "Files / Photos (folder)",
      files: dropped.map((item) => item.file),
    },
  ]);
});

it("waits for pending requests and stops scheduling after a creation failure", async () => {
  vi.spyOn(nodesApi, "lookupSiblingNames").mockResolvedValue({
    nodes: [],
    files: [],
    takenNameKeys: [],
  });
  const fail = vi.fn<(error: Error) => void>();
  const failure = new Promise<void>((_resolve, reject) => {
    fail.mockImplementation(reject);
  });
  const release = vi.fn<() => void>();
  const gate = new Promise<void>((resolve) => {
    release.mockImplementation(resolve);
  });
  const create = vi
    .spyOn(nodesApi, "createNode")
    .mockImplementation(async ({ parentId, name }) => {
      if (name === "Folder-0") {
        await failure;
      } else {
        await gate;
      }
      return createFolder({ id: name, parentId, name });
    });
  const dropped = Array.from({ length: 20 }, (_, index) => ({
    file: new File(["data"], `file-${index}.txt`),
    relativePath: `Folder-${index}/file-${index}.txt`,
  }));
  const pending = new DroppedFolderResolver(
    "root",
    "Files",
    false,
    () => false,
  ).groupFiles(dropped, () => {});
  const settled = vi.fn();
  void pending.then(settled, settled);

  await vi.waitFor(() => {
    expect(create).toHaveBeenCalledTimes(
      uploadConfig.maxConcurrentFolderCreations,
    );
  });
  const error = new Error("Creation failed");
  fail(error);
  await new Promise((resolve) => setTimeout(resolve, 0));
  expect(settled).not.toHaveBeenCalled();
  release();
  await expect(pending).rejects.toBe(error);
  expect(create).toHaveBeenCalledTimes(
    uploadConfig.maxConcurrentFolderCreations,
  );
});
