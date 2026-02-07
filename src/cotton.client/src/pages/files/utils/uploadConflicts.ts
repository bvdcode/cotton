import type { NodeContentDto } from "../../../shared/api/nodesApi";
import { nextAvailableName } from "./fileNameUtils";

export const ConflictAction = {
  Rename: "rename",
  Skip: "skip",
  SkipAll: "skipAll",
  Cancel: "cancel",
} as const;

export type ConflictAction =
  (typeof ConflictAction)[keyof typeof ConflictAction];

export interface ConflictResult {
  files: File[];
  cancelled: boolean;
}

/**
 * Resolve upload conflicts by checking which files have duplicate names
 * and prompting the user to rename or skip them.
 *
 * - Rename: rename the current file and keep going.
 * - SkipAll: stop asking, skip every remaining conflict.
 * - Cancel: abort the entire upload (returns cancelled = true).
 */
export async function resolveUploadConflicts(
  files: File[],
  content: NodeContentDto,
  confirmRename: (newName: string) => Promise<ConflictAction>,
): Promise<ConflictResult> {
  const takenLower = new Set<string>([
    ...content.nodes.map((n) => n.name.toLowerCase()),
    ...content.files.map((f) => f.name.toLowerCase()),
  ]);

  const resolved: File[] = [];
  let skipAllConflicts = false;

  for (const file of files) {
    const desiredNameLower = file.name.toLowerCase();
    if (!takenLower.has(desiredNameLower)) {
      takenLower.add(desiredNameLower);
      resolved.push(file);
      continue;
    }

    if (skipAllConflicts) {
      continue;
    }

    const newName = nextAvailableName(file.name, takenLower);
    const action = await confirmRename(newName);

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

    const renamed = new File([file], newName, {
      type: file.type,
      lastModified: file.lastModified,
    });
    takenLower.add(newName.toLowerCase());
    resolved.push(renamed);
  }

  return { files: resolved, cancelled: false };
}
