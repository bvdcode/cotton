import {
  hasEncryptedFolderName,
  readFolderName,
  applyDisplayMetaToNodes,
} from "../crypto/folderDisplayMeta";
import { prepareFolderName } from "../crypto/folderNameOperations";
import { useVault } from "../crypto/vault";
import {
  FolderTransferNames,
  DisplayNameConflictError,
} from "./FolderTransferNames";
import { isAxiosError } from "../api/httpClient";
import { fetchAllNodeChildren } from "../api/nodeChildren";
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
import { readFileDisplayMeta } from "../crypto/displayMeta";
import { transferFile } from "./transferFile";

export type MoveConflictResolver = (
  prompt: NameConflictPrompt,
) => Promise<ConflictAction>;

export type MoveSingleItemResult =
  | { kind: "folder"; folder: NodeDto }
  | { kind: "file"; file: NodeFileManifestDto };

export type MoveItemOutcome =
  | { kind: "moved"; moved: MoveSingleItemResult }
  | { kind: "merge"; targetId: string }
  | { kind: "failed"; error: Error }
  | { kind: "skipped"; skipAll: boolean }
  | { kind: "cancelled" };

const getMoveItemName = async (item: MoveClipboardItem): Promise<string> => {
  if (item.kind === "file") {
    if (!item.file) {
      throw new Error("File name is missing from the move item.");
    }
    return (await readFileDisplayMeta(item.file)).name;
  }

  const node = await nodesApi.getNode(item.id);
  return readFolderName(node);
};

const getTargetNames = async (targetParentId: string) => {
  const { content } = await fetchAllNodeChildren(targetParentId);
  const taken = new Set<string>();
  const folders = new Map<string, string>();

  for (const node of await applyDisplayMetaToNodes(content.nodes)) {
    taken.add(getFileNameKey(node.name));
    folders.set(getFileNameKey(node.name), node.id);
  }
  for (const file of content?.files ?? []) {
    const nameKey = getFileNameKey(file.name);
    taken.add(nameKey);
  }

  return { taken, folders };
};

const getMoveConflictKind = <T>(error: T): RestoreConflictKind | null => {
  if (error instanceof DisplayNameConflictError) {
    return error.kind;
  }
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
  encryptFolders: boolean,
  folderNames: FolderTransferNames,
): Promise<MoveSingleItemResult> => {
  if (item.kind === "folder") {
    const source = await nodesApi.getNode(item.id);
    const sourceEncrypted = hasEncryptedFolderName(source.metadata);
    const encrypted =
      sourceEncrypted || (operation === "copy" && encryptFolders);
    const request: MoveNodeRequest = { parentId: targetParentId };
    const displayName = name ?? (await readFolderName(source));
    if (encrypted || useVault.getState().isUnlocked) {
      await folderNames.check(
        displayName,
        operation === "move" ? item.id : undefined,
      );
    }
    if (encrypted && (operation === "copy" || name !== undefined)) {
      Object.assign(
        request,
        await prepareFolderName(source, displayName, true),
      );
      if (operation === "copy") {
        request.name = crypto.randomUUID();
      }
    } else if (name !== undefined) {
      request.name = name;
    }
    let folder: NodeDto;
    switch (operation) {
      case "move":
        folder = await nodesApi.moveNode(item.id, request);
        break;
      case "copy":
        folder = await nodesApi.copyNode(item.id, request);
        break;
    }
    folderNames.record(
      displayName,
      folder.id,
      "Folder",
      folder.name,
      encrypted,
    );
    return { kind: "folder", folder };
  }

  return {
    kind: "file",
    file: await transferFile(
      item,
      targetParentId,
      name,
      overwrite,
      operation,
      folderNames,
    ),
  };
};

interface MoveConflictOptions {
  operation?: FileTransferOperation;
  encryptFolders?: boolean;
  folderNames?: FolderTransferNames;
  confirmConflict: MoveConflictResolver;
  item: MoveClipboardItem;
  skipAllConflicts: boolean;
  targetParentId: string;
  hasMoreItems?: boolean;
}

interface MoveConflictState {
  name: string | undefined;
  overwrite: boolean;
  rejectedNames: Set<string>;
  targetNames: Set<string> | null;
  targetFolders: Map<string, string>;
}

type ConflictResolution = MoveItemOutcome | { kind: "retry" };

export const moveItemWithConflictResolution = async (
  options: MoveConflictOptions,
): Promise<MoveItemOutcome> => {
  const folderNames =
    options.folderNames ?? new FolderTransferNames(options.targetParentId);
  const state: MoveConflictState = {
    name: undefined,
    overwrite: false,
    rejectedNames: new Set<string>(),
    targetNames: null,
    targetFolders: new Map(),
  };

  while (true) {
    try {
      const moved = await moveSingleItem(
        options.item,
        options.targetParentId,
        state.name,
        state.overwrite,
        options.operation ?? "move",
        options.encryptFolders ?? false,
        folderNames,
      );
      return { kind: "moved", moved };
    } catch (error) {
      const resolution = await resolveMoveConflict(
        options,
        error,
        state,
        folderNames,
      );
      if (resolution.kind !== "retry") {
        return resolution;
      }
    }
  }
};

async function resolveMoveConflict<T>(
  options: MoveConflictOptions,
  error: T,
  state: MoveConflictState,
  folderNames: FolderTransferNames,
): Promise<ConflictResolution> {
  if (!isRetryableConflict(error, state.overwrite)) {
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

  try {
    if (state.targetNames === null) {
      if (error instanceof DisplayNameConflictError) {
        state.targetNames = await folderNames.takenNameKeys();
      } else {
        const names = await getTargetNames(options.targetParentId);
        state.targetNames = names.taken;
        state.targetFolders = names.folders;
      }
    }
  } catch (loadError) {
    return {
      kind: "failed",
      error:
        loadError instanceof Error
          ? loadError
          : new Error("Could not load target folder."),
    };
  }
  const conflictingName = state.name ?? originalName;
  state.rejectedNames.add(getFileNameKey(conflictingName));
  for (const rejectedName of state.rejectedNames) {
    state.targetNames.add(rejectedName);
  }

  const newName = nextAvailableName(originalName, state.targetNames);
  const targetId =
    options.item.kind === "folder" && getMoveConflictKind(error) === "Folder"
      ? error instanceof DisplayNameConflictError
        ? await folderNames.findFolderId(conflictingName)
        : state.targetFolders.get(getFileNameKey(conflictingName))
      : undefined;
  const canMerge = targetId !== undefined && targetId !== options.item.id;
  const action = await options.confirmConflict({
    newName,
    canOverwrite: canOverwriteConflict(options, error),
    canMerge,
    canApplyToAll: options.hasMoreItems === true,
  });

  switch (action) {
    case ConflictAction.Merge:
      if (!canMerge || targetId === undefined) {
        return {
          kind: "failed",
          error: new Error("No matching destination folder to merge."),
        };
      }
      return { kind: "merge", targetId };
    case ConflictAction.Cancel:
      return { kind: "cancelled" };
    case ConflictAction.Skip:
      return { kind: "skipped", skipAll: false };
    case ConflictAction.SkipAll:
      return { kind: "skipped", skipAll: true };
    case ConflictAction.Rename:
      state.name = newName;
      state.overwrite = false;
      return { kind: "retry" };
    case ConflictAction.Overwrite:
    case ConflictAction.OverwriteAll:
      state.name = conflictingName;
      state.overwrite = true;
      return { kind: "retry" };
  }
}

function isRetryableConflict<T>(error: T, overwrite: boolean): boolean {
  return (
    error instanceof DisplayNameConflictError ||
    (isAxiosError(error) && error.response?.status === 409 && !overwrite)
  );
}

function canOverwriteConflict<T>(
  options: MoveConflictOptions,
  error: T,
): boolean {
  return (
    options.item.kind === "file" &&
    getMoveConflictKind(error) === "File" &&
    (!(error instanceof DisplayNameConflictError) || error.canOverwrite) &&
    !(
      options.operation === "copy" &&
      options.item.sourceParentId === options.targetParentId
    )
  );
}
