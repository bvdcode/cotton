import type { NodeContentDto } from "../../../shared/api/nodesApi";
import type { UploadFileQueueItem } from "../../../shared/upload/types";
import {
  ConflictAction,
  type NameConflictPrompt,
} from "../../../shared/types/nameConflict";
import {
  getFileNameKey,
  nextAvailableName,
  normalizeFileName,
} from "../../../shared/utils/fileNameUtils";

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
  confirmConflict: (prompt: NameConflictPrompt) => Promise<ConflictAction>,
): Promise<ConflictResult> {
  const filesByNameKey = new Map(
    content.files.map((file) => [getFileNameKey(file.name), file]),
  );
  const takenNameKeys = new Set<string>([
    ...content.nodes.map((node) => getFileNameKey(node.name)),
    ...filesByNameKey.keys(),
  ]);

  const resolved: UploadFileQueueItem[] = [];
  const replacedFileIds = new Set<string>();
  let skipAllConflicts = false;

  for (const sourceFile of files) {
    const normalizedName = normalizeFileName(sourceFile.name);
    const file =
      normalizedName === sourceFile.name
        ? sourceFile
        : new File([sourceFile], normalizedName, {
            type: sourceFile.type,
            lastModified: sourceFile.lastModified,
          });
    const desiredNameKey = getFileNameKey(file.name);
    if (!takenNameKeys.has(desiredNameKey)) {
      takenNameKeys.add(desiredNameKey);
      resolved.push({ file });
      continue;
    }

    if (skipAllConflicts) {
      continue;
    }

    const newName = nextAvailableName(file.name, takenNameKeys);
    const existingFile = filesByNameKey.get(desiredNameKey);
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

    if (action === ConflictAction.Overwrite && existingFile && canOverwrite) {
      replacedFileIds.add(existingFile.id);
      resolved.push({ file, replaceNodeFileId: existingFile.id });
      continue;
    }

    const renamed = new File([file], newName, {
      type: file.type,
      lastModified: file.lastModified,
    });
    takenNameKeys.add(getFileNameKey(newName));
    resolved.push({ file: renamed });
  }

  return { files: resolved, cancelled: false };
}
