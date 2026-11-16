import type { UploadFileQueueItem } from "./types";
import type { EnqueueOptions } from "./UploadTaskTypes";

export type UploadQueueEntry = File | UploadFileQueueItem;

export interface UploadQueueBatch {
  files: FileList | UploadQueueEntry[];
  nodeId: string;
  nodeLabel: string;
  options?: EnqueueOptions;
}

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
