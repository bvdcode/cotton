import { isAxiosError } from "../api/httpClient";
import { fetchAllNodeChildren } from "../api/nodeChildren";
import { filesApi, type MoveFileRequest } from "../api/filesApi";
import type { NodeDto } from "../api/layoutsApi";
import {
  nodesApi,
  type MoveNodeRequest,
  type NodeFileManifestDto,
  type RestoreConflictKind,
} from "../api/nodesApi";
import type {
  FileTransferOperation,
  MoveClipboardItem,
} from "../store/moveClipboardStore";
import { ConflictAction, type NameConflictPrompt } from "../types/nameConflict";
import { getFileNameKey, nextAvailableName } from "../utils/fileNameUtils";
import { readStringProperty } from "../utils/typeGuards";
import { isFileEncrypted } from "../crypto";
import { createOpaqueServerFileName } from "../upload/uploadFileToNode";

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

const getTargetNames = async (targetParentId: string): Promise<Set<string>> => {
  const { content } = await fetchAllNodeChildren(targetParentId);
  const taken = new Set<string>();

  for (const node of content?.nodes ?? []) {
    taken.add(getFileNameKey(node.name));
  }
  for (const file of content?.files ?? []) {
    const nameKey = getFileNameKey(file.name);
    taken.add(nameKey);
  }

  return taken;
};

const getMoveConflictKind = <T>(error: T): RestoreConflictKind | null => {
  if (!isAxiosError(error) || error.response?.status !== 409) {
    return null;
  }

  const conflictKind = readStringProperty(error.response.data, "conflictKind");
  switch (conflictKind) {
    case "File":
    case "Folder":
      return conflictKind;
    default:
      return null;
  }
};

const moveSingleItem = async (
  item: MoveClipboardItem,
  targetParentId: string,
  name: string | undefined,
  overwrite: boolean,
  operation: FileTransferOperation,
): Promise<MoveSingleItemResult> => {
  if (item.kind === "folder") {
    const request: MoveNodeRequest = { parentId: targetParentId };
    if (name !== undefined) {
      request.name = name;
    }
    switch (operation) {
      case "move":
        return {
          kind: "folder",
          folder: await nodesApi.moveNode(item.id, request),
        };
      case "copy":
        return {
          kind: "folder",
          folder: await nodesApi.copyNode(item.id, request),
        };
    }
  }

  const request: MoveFileRequest = { parentId: targetParentId };
  if (name !== undefined) {
    request.name = name;
  }
  if (overwrite) {
    request.overwrite = true;
  }
  switch (operation) {
    case "move":
      return { kind: "file", file: await filesApi.moveFile(item.id, request) };
    case "copy":
      if (isFileEncrypted(item.file?.metadata)) {
        request.name = createOpaqueServerFileName();
      }
      return { kind: "file", file: await filesApi.copyFile(item.id, request) };
  }
};

export const moveItemWithConflictResolution = async (options: {
  operation?: FileTransferOperation;
  confirmConflict: MoveConflictResolver;
  item: MoveClipboardItem;
  skipAllConflicts: boolean;
  targetParentId: string;
}): Promise<MoveItemOutcome> => {
  let name: string | undefined;
  let overwrite = false;
  const rejectedNames = new Set<string>();
  let targetNames: Set<string> | null = null;

  while (true) {
    try {
      const moved = await moveSingleItem(
        options.item,
        options.targetParentId,
        name,
        overwrite,
        options.operation ?? "move",
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

      const conflictKind = getMoveConflictKind(error);
      try {
        targetNames ??= await getTargetNames(options.targetParentId);
      } catch (loadError) {
        return {
          kind: "failed",
          error:
            loadError instanceof Error
              ? loadError
              : new Error("Could not load target folder."),
        };
      }
      const conflictingName = name ?? originalName;
      const conflictingNameKey = getFileNameKey(conflictingName);
      rejectedNames.add(conflictingNameKey);
      for (const rejectedName of rejectedNames) {
        targetNames.add(rejectedName);
      }

      const newName = nextAvailableName(originalName, targetNames);
      const action = await options.confirmConflict({
        newName,
        canOverwrite:
          options.item.kind === "file" &&
          conflictKind === "File" &&
          !(
            options.operation === "copy" &&
            options.item.sourceParentId === options.targetParentId
          ),
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
