import { nodesApi, type NodeFileManifestDto } from "../api/nodesApi";
import { isFileEncrypted } from "../crypto/metadataFlags";

export const CLIENT_ENCRYPTION_FOLDER_SCAN_MAX_FILES = 500;
export const CLIENT_ENCRYPTION_FOLDER_SCAN_MAX_FOLDERS = 250;
const CLIENT_ENCRYPTION_FOLDER_SCAN_PAGE_SIZE = 250;

export interface ClientEncryptionFolderScanResult {
  files: NodeFileManifestDto[];
  scannedFolders: number;
  truncated: boolean;
}

type FilePredicate = (file: NodeFileManifestDto) => boolean;

interface ScanOptions {
  maxFiles?: number;
  maxFolders?: number;
}

export const collectPlainFilesInFoldersForClientEncryption = (
  rootNodeIds: ReadonlyArray<string>,
  options: ScanOptions = {},
): Promise<ClientEncryptionFolderScanResult> =>
  collectFilesInFolders(rootNodeIds, (file) => !isFileEncrypted(file.metadata), options);

export const collectEncryptedFilesInFoldersForClientEncryption = (
  rootNodeIds: ReadonlyArray<string>,
  options: ScanOptions = {},
): Promise<ClientEncryptionFolderScanResult> =>
  collectFilesInFolders(rootNodeIds, (file) => isFileEncrypted(file.metadata), options);

async function collectFilesInFolders(
  rootNodeIds: ReadonlyArray<string>,
  predicate: FilePredicate,
  options: ScanOptions,
): Promise<ClientEncryptionFolderScanResult> {
  const maxFiles = Math.max(1, options.maxFiles ?? CLIENT_ENCRYPTION_FOLDER_SCAN_MAX_FILES);
  const maxFolders = Math.max(1, options.maxFolders ?? CLIENT_ENCRYPTION_FOLDER_SCAN_MAX_FOLDERS);
  const files: NodeFileManifestDto[] = [];
  const visited = new Set<string>();
  const queue: string[] = [];

  for (const nodeId of rootNodeIds) {
    if (visited.has(nodeId)) continue;
    visited.add(nodeId);
    queue.push(nodeId);
  }

  let scannedFolders = 0;
  let truncated = false;

  while (queue.length > 0) {
    if (scannedFolders >= maxFolders || files.length >= maxFiles) {
      truncated = true;
      break;
    }

    const nodeId = queue.shift()!;
    scannedFolders += 1;
    let page = 1;
    let seenChildren = 0;

    while (files.length < maxFiles) {
      const response = await nodesApi.getChildren(nodeId, {
        page,
        pageSize: CLIENT_ENCRYPTION_FOLDER_SCAN_PAGE_SIZE,
      });
      const childCount = response.content.nodes.length + response.content.files.length;
      seenChildren += childCount;

      for (const childNode of response.content.nodes) {
        if (queue.length + scannedFolders >= maxFolders) {
          truncated = true;
          continue;
        }

        if (!visited.has(childNode.id)) {
          visited.add(childNode.id);
          queue.push(childNode.id);
        }
      }

      for (const file of response.content.files) {
        if (!predicate(file)) continue;

        files.push(file);
        if (files.length >= maxFiles) {
          truncated = true;
          break;
        }
      }

      if (childCount === 0 || seenChildren >= response.totalCount) {
        break;
      }

      page += 1;
    }
  }

  return { files, scannedFolders, truncated };
}
