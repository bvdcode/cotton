import { describe, expect, it } from "vitest";
import { getAllFilesFromEntries, getDroppedEntries } from "./scanDroppedFiles";

const getAllFilesFromItems = (
  items: DataTransferItemList,
  onFileFound: (count: number) => void,
) => getAllFilesFromEntries(getDroppedEntries(items), onFileFound);

const unusedEntryMethod = (): never => {
  throw new Error("Unexpected filesystem operation.");
};

const fileEntry = (path: string, missing = false): FileSystemFileEntry => {
  const file = new File(["photo"], path.split("/").at(-1) ?? "photo.jpg", {
    type: "image/jpeg",
    lastModified: 123,
  });
  const entry: FileSystemFileEntry = {
    isFile: true,
    isDirectory: false,
    name: file.name,
    fullPath: path,
    get filesystem(): FileSystem {
      return unusedEntryMethod();
    },
    getParent: unusedEntryMethod,
    file: (success, failure) => {
      if (missing) {
        failure?.(new DOMException("Missing file", "NotFoundError"));
        return;
      }
      success(file);
    },
  };
  Object.defineProperty(entry, "filesystem", { enumerable: false });
  Object.defineProperty(entry, "fullPath", { enumerable: false });
  return entry;
};

const directoryEntry = (
  path: string,
  batches: FileSystemEntry[][],
): FileSystemDirectoryEntry => ({
  isFile: false,
  isDirectory: true,
  name: path.split("/").at(-1) ?? "Photos",
  fullPath: path,
  get filesystem(): FileSystem {
    return unusedEntryMethod();
  },
  getParent: unusedEntryMethod,
  getDirectory: unusedEntryMethod,
  getFile: unusedEntryMethod,
  createReader: () => {
    let index = 0;
    return { readEntries: (success) => success(batches[index++] ?? []) };
  },
});

const droppedItems = (entry: FileSystemEntry): DataTransferItemList => ({
  0: {
    kind: "file",
    type: "",
    getAsFile: () => null,
    getAsString: () => {},
    webkitGetAsEntry: () => entry,
  },
  length: 1,
  add: () => null,
  clear: () => {},
  remove: () => {},
  [Symbol.iterator]() {
    return [this[0]][Symbol.iterator]();
  },
});

describe("dropped directory paths", () => {
  it("retains the filesystem File objects without wrapping their content in new files", async () => {
    const entries = [
      fileEntry("/Photos/one.jpg"),
      fileEntry("/Photos/two.jpg"),
    ];
    const originals: File[] = [];
    for (const entry of entries) {
      entry.file((file) => originals.push(file));
    }
    const root = directoryEntry("/Photos", [entries]);

    const result = await getAllFilesFromItems(droppedItems(root), () => {});

    expect(result.files).toHaveLength(originals.length);
    for (let index = 0; index < originals.length; index += 1) {
      expect(result.files[index].file).toBe(originals[index]);
    }
  });

  it("keeps the top directory and nested paths for native entry properties", async () => {
    const first = fileEntry("/Photos/one.jpg");
    const nested = directoryEntry("/Photos/Trip", [
      [fileEntry("/Photos/Trip/two.jpg")],
    ]);
    const root = directoryEntry("/Photos", [[first], [nested]]);

    expect(Object.entries(first).some(([key]) => key === "fullPath")).toBe(
      false,
    );
    const result = await getAllFilesFromItems(droppedItems(root), () => {});

    expect(result.files.map((item) => item.relativePath)).toEqual([
      "Photos/one.jpg",
      "Photos/Trip/two.jpg",
    ]);
    expect(result.files[0].file).toMatchObject({
      name: "one.jpg",
      type: "image/jpeg",
      size: 5,
      lastModified: 123,
    });
  });

  it("retains the full path of missing entries in the skipped list", async () => {
    const root = directoryEntry("/Photos", [
      [fileEntry("/Photos/Trip/missing.jpg", true)],
    ]);

    const result = await getAllFilesFromItems(droppedItems(root), () => {});

    expect(result).toEqual({
      files: [],
      skippedNotFound: 1,
      skippedItems: ["Photos/Trip/missing.jpg"],
    });
  });

  it("keeps standalone files in the current folder", async () => {
    const result = await getAllFilesFromItems(
      droppedItems(fileEntry("/one.jpg")),
      () => {},
    );

    expect(result.files[0].relativePath).toBe("one.jpg");
  });

  it("finishes outstanding reads before reporting a scan failure", async () => {
    const broken = fileEntry("/broken.jpg");
    const blocked = fileEntry("/blocked.jpg");
    const error = new DOMException("Cannot read file", "NotReadableError");
    broken.file = (_success, failure) => failure?.(error);
    let release!: () => void;
    blocked.file = (success) => {
      release = () => success(new File(["data"], "blocked.jpg"));
    };
    let settled = false;
    const pending = getAllFilesFromEntries([broken, blocked], () => {}).catch(
      (failure: Error) => {
        settled = true;
        return failure;
      },
    );
    await Promise.resolve();
    await Promise.resolve();
    expect(settled).toBe(false);
    release();
    expect(await pending).toBe(error);
  });
});
