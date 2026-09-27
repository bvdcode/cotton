import { readStringProperty } from "../../../shared/utils/typeGuards";

export type DroppedFile = {
  file: File;
  relativePath: string;
};

const maxSkippedItemsToKeep = 500;

export type DroppedScanResult = {
  files: DroppedFile[];
  skippedNotFound: number;
  skippedItems: string[];
};

const isNotFoundError = <T>(error: T): boolean => {
  if (error instanceof DOMException) return error.name === "NotFoundError";
  if (error instanceof Error) return error.name === "NotFoundError";
  return readStringProperty(error, "name") === "NotFoundError";
};

const isFileEntry = (entry: FileSystemEntry): entry is FileSystemFileEntry =>
  entry.isFile;

const isDirectoryEntry = (
  entry: FileSystemEntry,
): entry is FileSystemDirectoryEntry => entry.isDirectory;

export const getAllFilesFromItems = async (
  items: DataTransferItemList,
  onFileFound: (filesFound: number) => void,
): Promise<DroppedScanResult> => {
  const files: DroppedFile[] = [];
  const skippedItems: string[] = [];
  let skippedNotFound = 0;

  const rememberSkippedItem = (entry: FileSystemEntry) => {
    if (skippedItems.length >= maxSkippedItemsToKeep) return;
    const fullPath = readStringProperty(entry, "fullPath");
    const display = (fullPath ?? entry.name).replace(/^\/+/, "").trim();
    if (display.length === 0) return;
    skippedItems.push(display);
  };

  let lastNotifiedCount = 0;
  let lastNotifyTime = 0;
  const notify = () => {
    const now = Date.now();
    const count = files.length;
    if (count === lastNotifiedCount) return;
    if (now - lastNotifyTime < 120) return;
    lastNotifiedCount = count;
    lastNotifyTime = now;
    onFileFound(count);
  };

  const traverseEntry = async (entry: FileSystemEntry): Promise<void> => {
    if (isFileEntry(entry)) {
      let file: File;
      try {
        file = await new Promise<File>((resolve, reject) => {
          entry.file(resolve, reject);
        });
      } catch (e) {
        if (isNotFoundError(e)) {
          skippedNotFound += 1;
          rememberSkippedItem(entry);
          return;
        }
        throw e;
      }
      const clonedFile = new File([file], file.name, {
        type: file.type,
        lastModified: file.lastModified,
      });

      const fullPath = readStringProperty(entry, "fullPath");
      const relativePath = (fullPath ?? file.name).replace(/^\/+/, "");
      files.push({ file: clonedFile, relativePath });
      notify();
    } else if (isDirectoryEntry(entry)) {
      const reader = entry.createReader();

      const readAllEntries = async (): Promise<FileSystemEntry[]> => {
        const allEntries: FileSystemEntry[] = [];
        let batch: FileSystemEntry[];

        do {
          batch = await new Promise<FileSystemEntry[]>((resolve, reject) => {
            reader.readEntries(resolve, reject);
          });
          allEntries.push(...batch);
        } while (batch.length > 0);

        return allEntries;
      };

      let entries: FileSystemEntry[];
      try {
        entries = await readAllEntries();
      } catch (e) {
        if (isNotFoundError(e)) {
          skippedNotFound += 1;
          rememberSkippedItem(entry);
          return;
        }
        throw e;
      }
      for (const childEntry of entries) {
        await traverseEntry(childEntry);
      }
    }
  };

  const promises: Promise<void>[] = [];
  for (let i = 0; i < items.length; i++) {
    const item = items[i];
    if (item.kind === "file") {
      const entry = item.webkitGetAsEntry();
      if (entry) {
        promises.push(traverseEntry(entry));
      }
    }
  }
  await Promise.all(promises);

  return { files, skippedNotFound, skippedItems };
};
