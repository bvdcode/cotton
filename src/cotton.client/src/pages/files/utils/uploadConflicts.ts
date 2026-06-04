import type { NodeContentDto } from "../../../shared/api/nodesApi";
import type { UploadFileQueueItem } from "../../../shared/upload/types";
import { nextAvailableName } from "./fileNameUtils";

export const ConflictAction = {
  Overwrite: "overwrite",
  Rename: "rename",
  Skip: "skip",
  SkipAll: "skipAll",
  Cancel: "cancel",
} as const;

export type ConflictAction =
  (typeof ConflictAction)[keyof typeof ConflictAction];

export interface UploadConflictPrompt {
  newName: string;
  canOverwrite: boolean;
}

export interface ConflictResult {
  files: UploadFileQueueItem[];
  cancelled: boolean;
}

/**
 * Resolve upload conflicts by checking which files have duplicate names
 * and prompting the user to overwrite, rename, or skip them.
 *
 * - Overwrite: replace the existing file content.
 * - Rename: rename the current file and keep going.
 * - SkipAll: stop asking, skip every remaining conflict.
 * - Cancel: abort the entire upload (returns cancelled = true).
 */
export async function resolveUploadConflicts(
  files: File[],
  content: NodeContentDto,
  confirmConflict: (prompt: UploadConflictPrompt) => Promise<ConflictAction>,
): Promise<ConflictResult> {
  const filesByNameLower = new Map(
    content.files.map((file) => [file.name.toLowerCase(), file]),
  );
  const takenLower = new Set<string>([
    ...content.nodes.map((n) => n.name.toLowerCase()),
    ...filesByNameLower.keys(),
  ]);

  const resolved: UploadFileQueueItem[] = [];
  const replacedFileIds = new Set<string>();
  let skipAllConflicts = false;

  for (const file of files) {
    const desiredNameLower = file.name.toLowerCase();
    if (!takenLower.has(desiredNameLower)) {
      takenLower.add(desiredNameLower);
      resolved.push({ file });
      continue;
    }

    if (skipAllConflicts) {
      continue;
    }

    const newName = nextAvailableName(file.name, takenLower);
    const existingFile = filesByNameLower.get(desiredNameLower);
    const canOverwrite = Boolean(
      existingFile && !replacedFileIds.has(existingFile.id),
    );
    const action = await confirmConflict({ newName, canOverwrite });

    if (action === ConflictAction.Cancel) {
      return { files: [], cancelled: true };
    }

    if (action === ConflictAction.Skip) {
      continue;
    }

    if (action === ConflictAction.SkipAll) {
      skipAllConflicts = true;
      continue;
    }

    if (
      action === ConflictAction.Overwrite &&
      existingFile &&
      canOverwrite
    ) {
      replacedFileIds.add(existingFile.id);
      resolved.push({ file, replaceNodeFileId: existingFile.id });
      continue;
    }

    const renamed = new File([file], newName, {
      type: file.type,
      lastModified: file.lastModified,
    });
    takenLower.add(newName.toLowerCase());
    resolved.push({ file: renamed });
  }

  return { files: resolved, cancelled: false };
}
