import type { UploadFileQueueItem } from "./types";

export type UploadQueueEntry = File | UploadFileQueueItem;

export const normalizeUploadQueueEntries = (
  files: FileList | UploadQueueEntry[],
): UploadFileQueueItem[] => {
  const list = Array.isArray(files) ? files : Array.from(files);
  return list.map((entry) => {
    if ("file" in entry) {
      return entry;
    }

    return { file: entry };
  });
};
