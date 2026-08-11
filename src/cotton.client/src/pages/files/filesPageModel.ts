import { readEnvelopeFromPreferences } from "../../shared/crypto";
import type { FileSystemTile } from "../../shared/types/FileListViewTypes";
import { downloadArchive } from "../../shared/utils/fileHandlers";

const HUGE_FOLDER_THRESHOLD = 100_000;

export type ClientEncryptionFolderAction =
  | "encrypt-existing"
  | "decrypt-existing";

export type ClientEncryptionUnlockPrompt =
  | { kind: "current" }
  | { kind: "open"; folderId: string }
  | { kind: "action"; action: ClientEncryptionFolderAction };

export type FolderEncryptionPromptModel = {
  severity: "info" | "warning";
  message: string;
  action: string;
  disabled: boolean;
  onAction: () => void;
};

export type ArchiveDownloadRequest = Parameters<typeof downloadArchive>[0];

const normalizeSiblingName = (name: string): string =>
  name.trim().toLocaleLowerCase();

export const buildUniqueSiblingName = (
  baseName: string,
  siblingNames: ReadonlyArray<string>,
): string => {
  const normalizedNames = new Set(siblingNames.map(normalizeSiblingName));
  if (!normalizedNames.has(normalizeSiblingName(baseName))) {
    return baseName;
  }

  const extensionIndex = baseName.lastIndexOf(".");
  const hasExtension = extensionIndex > 0;
  const nameWithoutExtension = hasExtension
    ? baseName.slice(0, extensionIndex)
    : baseName;
  const extension = hasExtension ? baseName.slice(extensionIndex) : "";

  for (let index = 1; index < 1000; index += 1) {
    const candidate = `${nameWithoutExtension} ${index}${extension}`;
    if (!normalizedNames.has(normalizeSiblingName(candidate))) {
      return candidate;
    }
  }

  return `${nameWithoutExtension} ${Date.now()}${extension}`;
};

export const isEditableKeyboardTarget = (
  target: EventTarget | null,
): boolean => {
  if (!(target instanceof HTMLElement)) {
    return false;
  }

  const tagName = target.tagName.toLocaleLowerCase();
  return (
    target.isContentEditable ||
    tagName === "input" ||
    tagName === "textarea" ||
    tagName === "select"
  );
};

export const isCreateFolderShortcut = (event: KeyboardEvent): boolean =>
  (event.ctrlKey || event.metaKey) &&
  event.shiftKey &&
  (event.code === "KeyN" || event.key.toLocaleLowerCase() === "n");

const tileId = (tile: FileSystemTile): string =>
  tile.kind === "folder" ? tile.node.id : tile.file.id;

export const resolveFilesNodeId = (
  routeNodeId: string | undefined,
  rootNodeId: string | null,
): string | null => routeNodeId ?? rootNodeId ?? null;

export const getCurrentContent = <TContent,>(
  nodeId: string | null,
  isUserCacheValid: boolean,
  contentByNodeId: Record<string, TContent>,
): TContent | undefined =>
  nodeId && isUserCacheValid ? contentByNodeId[nodeId] : undefined;

export const isHugeFolderCount = (
  childrenTotalCount: number | null,
): boolean =>
  childrenTotalCount !== null && childrenTotalCount > HUGE_FOLDER_THRESHOLD;

export const getActiveCurrentNode = <TNode extends { id: string }>(
  nodeId: string | null,
  currentNode: TNode | null | undefined,
): TNode | null => (nodeId && currentNode?.id === nodeId ? currentNode : null);

export const getGoUpParentId = (
  ancestors: ReadonlyArray<{ id: string }>,
): string | null =>
  ancestors.length > 0 ? ancestors[ancestors.length - 1].id : null;

export const shouldPromptForCurrentFolderUnlock = (options: {
  clientEncryptionEnabled: boolean;
  currentNodeId?: string | null;
  isVaultUnlocked: boolean;
  nodeId: string | null;
}): boolean =>
  Boolean(options.nodeId && options.currentNodeId === options.nodeId) &&
  !options.isVaultUnlocked &&
  options.clientEncryptionEnabled;

export const isFilesUnlockDialogOpen = (
  prompt: ClientEncryptionUnlockPrompt | null,
  envelope: ReturnType<typeof readEnvelopeFromPreferences>,
): boolean => prompt !== null && envelope !== null;

export const shouldRenderFilesList = <TContent,>(
  error: string | null,
  content: TContent | null | undefined,
): boolean => !error || (content !== null && content !== undefined);

export const buildFolderEncryptionPrompt = (options: {
  decryptEncryptedFiles: () => void;
  encryptedFilesCount: number;
  encryptedFilesMessage: string;
  encryptedFilesAction: string;
  encryptPlainFiles: () => void;
  folderPolicyEnabled: boolean;
  isDecryptingEncryptedFiles: boolean;
  isEncryptingPlainFiles: boolean;
  plainFilesCount: number;
  plainFilesMessage: string;
  plainFilesAction: string;
}): FolderEncryptionPromptModel | null => {
  if (
    options.folderPolicyEnabled &&
    options.plainFilesCount > 0 &&
    !options.isEncryptingPlainFiles
  ) {
    return {
      severity: "warning",
      message: options.plainFilesMessage,
      action: options.plainFilesAction,
      disabled: false,
      onAction: () => {
        void options.encryptPlainFiles();
      },
    };
  }

  if (
    !options.folderPolicyEnabled &&
    options.encryptedFilesCount > 0 &&
    !options.isDecryptingEncryptedFiles
  ) {
    return {
      severity: "info",
      message: options.encryptedFilesMessage,
      action: options.encryptedFilesAction,
      disabled: false,
      onAction: () => {
        void options.decryptEncryptedFiles();
      },
    };
  }

  return null;
};

export const buildSelectionArchiveRequest = (
  tiles: ReadonlyArray<FileSystemTile>,
  selectedIds: ReadonlySet<string>,
  currentFolderName?: string | null,
): ArchiveDownloadRequest | null => {
  const selectedTiles = tiles.filter((tile) => selectedIds.has(tileId(tile)));
  if (selectedTiles.length === 0) {
    return null;
  }

  const fileIds = selectedTiles.flatMap((tile) =>
    tile.kind === "file" ? [tile.file.id] : [],
  );
  const nodeIds = selectedTiles.flatMap((tile) =>
    tile.kind === "folder" ? [tile.node.id] : [],
  );

  let archiveName = currentFolderName ?? undefined;
  if (selectedTiles.length === 1) {
    const selectedTile = selectedTiles[0];
    archiveName = selectedTile.kind === "folder"
      ? selectedTile.node.name
      : selectedTile.file.name;
  }

  return { fileIds, nodeIds, archiveName };
};
