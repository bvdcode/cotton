import { isAxiosError } from "../api/httpClient";
import { filesApi, type MoveFileRequest } from "../api/filesApi";
import type { NodeDto } from "../api/layoutsApi";
import {
  nodesApi,
  type MoveNodeRequest,
  type NodeFileManifestDto,
} from "../api/nodesApi";
import type { MoveClipboardItem } from "../store/moveClipboardStore";
import { useNodesStore } from "../store/nodesStore";
import { ConflictAction, type NameConflictPrompt } from "../types/nameConflict";
import { getFileNameKey, nextAvailableName } from "../utils/fileNameUtils";

export type MoveConflictResolver = (
  prompt: NameConflictPrompt,
) => Promise<ConflictAction>;

export type MoveSingleItemResult =
  | { kind: "folder"; folder: NodeDto }
  | { kind: "file"; file: NodeFileManifestDto };

export type MoveItemOutcome =
  | { kind: "moved"; moved: MoveSingleItemResult }
  | { kind: "failed"; error: Error }
  | { kind: "skipped"; skipAll: boolean }
  | { kind: "cancelled" };

const getMoveItemName = async (item: MoveClipboardItem): Promise<string> => {
  if (item.kind === "file") {
    if (!item.file) {
      throw new Error("File name is missing from the move item.");
    }
    return item.file.name;
  }

  const node = await nodesApi.getNode(item.id);
  return node.name;
};

const getCachedTargetNames = (
  targetParentId: string,
): { taken: Set<string>; files: Set<string> } => {
  const content = useNodesStore.getState().contentByNodeId[targetParentId];
  const taken = new Set<string>();
  const files = new Set<string>();

  for (const node of content?.nodes ?? []) {
    taken.add(getFileNameKey(node.name));
  }
  for (const file of content?.files ?? []) {
    const nameKey = getFileNameKey(file.name);
    taken.add(nameKey);
    files.add(nameKey);
  }

  return { taken, files };
};

const moveSingleItem = async (
  item: MoveClipboardItem,
  targetParentId: string,
  name: string | undefined,
  overwrite: boolean,
): Promise<MoveSingleItemResult> => {
  if (item.kind === "folder") {
    const request: MoveNodeRequest = { parentId: targetParentId };
    if (name !== undefined) {
      request.name = name;
    }
    const folder = await nodesApi.moveNode(item.id, request);
    return { kind: "folder", folder };
  }

  const request: MoveFileRequest = { parentId: targetParentId };
  if (name !== undefined) {
    request.name = name;
  }
  if (overwrite) {
    request.overwrite = true;
  }
  const file = await filesApi.moveFile(item.id, request);
  return { kind: "file", file };
};

export const moveItemWithConflictResolution = async (options: {
  confirmConflict: MoveConflictResolver;
  item: MoveClipboardItem;
  skipAllConflicts: boolean;
  targetParentId: string;
}): Promise<MoveItemOutcome> => {
  let name: string | undefined;
  let overwrite = false;
  const rejectedNames = new Set<string>();

  while (true) {
    try {
      const moved = await moveSingleItem(
        options.item,
        options.targetParentId,
        name,
        overwrite,
      );
      return { kind: "moved", moved };
    } catch (error) {
      if (!isAxiosError(error) || error.response?.status !== 409 || overwrite) {
        return {
          kind: "failed",
          error: error instanceof Error ? error : new Error("Move failed."),
        };
      }

      if (options.skipAllConflicts) {
        return { kind: "skipped", skipAll: true };
      }

      let originalName: string;
      try {
        originalName = await getMoveItemName(options.item);
      } catch (nameError) {
        return {
          kind: "failed",
          error:
            nameError instanceof Error
              ? nameError
              : new Error("Could not determine the item name."),
        };
      }

      const targetNames = getCachedTargetNames(options.targetParentId);
      const conflictingName = name ?? originalName;
      const conflictingNameKey = getFileNameKey(conflictingName);
      rejectedNames.add(conflictingNameKey);
      for (const rejectedName of rejectedNames) {
        targetNames.taken.add(rejectedName);
      }

      const newName = nextAvailableName(originalName, targetNames.taken);
      const action = await options.confirmConflict({
        newName,
        canOverwrite:
          options.item.kind === "file" &&
          targetNames.files.has(conflictingNameKey),
      });

      switch (action) {
        case ConflictAction.Cancel:
          return { kind: "cancelled" };
        case ConflictAction.Skip:
          return { kind: "skipped", skipAll: false };
        case ConflictAction.SkipAll:
          return { kind: "skipped", skipAll: true };
        case ConflictAction.Rename:
          name = newName;
          overwrite = false;
          break;
        case ConflictAction.Overwrite:
          name = conflictingName;
          overwrite = true;
          break;
      }
    }
  }
};
