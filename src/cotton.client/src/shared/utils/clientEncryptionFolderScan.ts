import type { NodeDto } from "../api/layoutsApi";
import { nodesApi, type NodeFileManifestDto } from "../api/nodesApi";
import { isFileEncrypted } from "../crypto/metadataFlags";

export const CLIENT_ENCRYPTION_FOLDER_SCAN_MAX_FILES = 500;
export const CLIENT_ENCRYPTION_FOLDER_SCAN_MAX_FOLDERS = 250;
const CLIENT_ENCRYPTION_FOLDER_SCAN_PAGE_SIZE = 250;

export interface ClientEncryptionFolderScanResult {
  folders: NodeDto[];
  files: NodeFileManifestDto[];
  scannedFolders: number;
  truncated: boolean;
}

type FilePredicate = (file: NodeFileManifestDto) => boolean;

interface ScanOptions {
  maxFiles?: number;
  maxFolders?: number;
}

interface ScanLimits {
  maxFiles: number;
  maxFolders: number;
}

interface ScanState {
  folders: NodeDto[];
  files: NodeFileManifestDto[];
  scannedFolders: number;
  truncated: boolean;
  visited: Set<string>;
  queue: string[];
}

export const collectPlainFilesInFoldersForClientEncryption = (
  rootNodeIds: ReadonlyArray<string>,
  options: ScanOptions = {},
): Promise<ClientEncryptionFolderScanResult> =>
  collectFilesInFolders(
    rootNodeIds,
    (file) => !isFileEncrypted(file.metadata),
    options,
  );

export const collectEncryptedFilesInFoldersForClientEncryption = (
  rootNodeIds: ReadonlyArray<string>,
  options: ScanOptions = {},
): Promise<ClientEncryptionFolderScanResult> =>
  collectFilesInFolders(
    rootNodeIds,
    (file) => isFileEncrypted(file.metadata),
    options,
  );

export const collectFoldersInFoldersForClientEncryption = (
  rootNodeIds: ReadonlyArray<string>,
  options: ScanOptions = {},
): Promise<ClientEncryptionFolderScanResult> =>
  collectFilesInFolders(rootNodeIds, () => false, options);

async function collectFilesInFolders(
  rootNodeIds: ReadonlyArray<string>,
  predicate: FilePredicate,
  options: ScanOptions,
): Promise<ClientEncryptionFolderScanResult> {
  const limits = resolveScanLimits(options);
  const state = createScanState(rootNodeIds);

  while (state.queue.length > 0 && !isScanLimitReached(state, limits)) {
    await scanNextFolder(state, limits, predicate);
  }

  if (state.queue.length > 0 || isScanLimitReached(state, limits)) {
    state.truncated = true;
  }

  return {
    folders: state.folders,
    files: state.files,
    scannedFolders: state.scannedFolders,
    truncated: state.truncated,
  };
}

const resolveScanLimits = (options: ScanOptions): ScanLimits => ({
  maxFiles: Math.max(
    1,
    options.maxFiles ?? CLIENT_ENCRYPTION_FOLDER_SCAN_MAX_FILES,
  ),
  maxFolders: Math.max(
    1,
    options.maxFolders ?? CLIENT_ENCRYPTION_FOLDER_SCAN_MAX_FOLDERS,
  ),
});

const createScanState = (rootNodeIds: ReadonlyArray<string>): ScanState => {
  const state: ScanState = {
    folders: [],
    files: [],
    scannedFolders: 0,
    truncated: false,
    visited: new Set<string>(),
    queue: [],
  };

  for (const nodeId of rootNodeIds) {
    enqueueFolderIfNew(state, nodeId);
  }

  return state;
};

const enqueueFolderIfNew = (state: ScanState, nodeId: string): boolean => {
  if (state.visited.has(nodeId)) {
    return false;
  }

  state.visited.add(nodeId);
  state.queue.push(nodeId);
  return true;
};

const isScanLimitReached = (state: ScanState, limits: ScanLimits): boolean =>
  state.scannedFolders >= limits.maxFolders ||
  state.files.length >= limits.maxFiles;

async function scanNextFolder(
  state: ScanState,
  limits: ScanLimits,
  predicate: FilePredicate,
): Promise<void> {
  const nodeId = state.queue.shift();
  if (!nodeId) {
    return;
  }

  state.scannedFolders += 1;
  let page = 1;
  let seenChildren = 0;

  while (state.files.length < limits.maxFiles) {
    const response = await nodesApi.getChildren(nodeId, {
      page,
      pageSize: CLIENT_ENCRYPTION_FOLDER_SCAN_PAGE_SIZE,
    });
    const childCount =
      response.content.nodes.length + response.content.files.length;
    seenChildren += childCount;

    enqueueChildFolders(state, limits, response.content.nodes);
    collectMatchingFiles(state, limits, response.content.files, predicate);

    if (isLastFolderPage(childCount, seenChildren, response.totalCount)) {
      return;
    }

    page += 1;
  }
}

const enqueueChildFolders = (
  state: ScanState,
  limits: ScanLimits,
  nodes: ReadonlyArray<NodeDto>,
): void => {
  for (const childNode of nodes) {
    if (state.queue.length + state.scannedFolders >= limits.maxFolders) {
      state.truncated = true;
      continue;
    }

    if (enqueueFolderIfNew(state, childNode.id)) {
      state.folders.push(childNode);
    }
  }
};

const collectMatchingFiles = (
  state: ScanState,
  limits: ScanLimits,
  files: ReadonlyArray<NodeFileManifestDto>,
  predicate: FilePredicate,
): void => {
  for (const file of files) {
    if (!predicate(file)) {
      continue;
    }

    state.files.push(file);
    if (state.files.length >= limits.maxFiles) {
      state.truncated = true;
      return;
    }
  }
};

const isLastFolderPage = (
  childCount: number,
  seenChildren: number,
  totalCount: number,
): boolean => childCount === 0 || seenChildren >= totalCount;
