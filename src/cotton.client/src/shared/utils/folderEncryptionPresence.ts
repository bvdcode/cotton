import type { NodeDto } from "../api/layoutsApi";
import type { NodeContentDto, NodeFileManifestDto } from "../api/nodesApi";
import { hasEncryptedFolderName } from "../crypto/folderDisplayMeta";
import {
  isFileEncrypted,
  isFolderEncryptionPolicyEnabled,
} from "../crypto/metadataFlags";
import { readFolderPages, type FolderScanPage } from "./folderPages";

export type FolderEncryptionMatch =
  "plain-files" | "encrypted-files" | "encrypted-names";

export function matchesFileEncryption(
  file: NodeFileManifestDto,
  match: FolderEncryptionMatch,
): boolean {
  switch (match) {
    case "plain-files":
      return !isFileEncrypted(file.metadata);
    case "encrypted-files":
    case "encrypted-names":
      return isFileEncrypted(file.metadata);
  }
}

export function matchesFolderPage(
  content: NodeContentDto,
  match: FolderEncryptionMatch,
): boolean {
  return (
    content.files.some((file) => matchesFileEncryption(file, match)) ||
    (match === "encrypted-names" &&
      content.nodes.some((node) => hasEncryptedFolderName(node.metadata)))
  );
}

function shouldScanChildFolder(
  node: NodeDto,
  match: FolderEncryptionMatch,
): boolean {
  switch (match) {
    case "plain-files":
      return true;
    case "encrypted-files":
      return !isFolderEncryptionPolicyEnabled(node.metadata);
    case "encrypted-names":
      return false;
  }
}

export async function findFolderEncryptionMatch(
  nodeId: string,
  match: FolderEncryptionMatch,
  signal: AbortSignal,
  firstPage?: FolderScanPage,
): Promise<boolean> {
  const folders = new Set([nodeId]);
  for (const current of folders) {
    for await (const response of readFolderPages(
      current,
      signal,
      current === nodeId ? firstPage : undefined,
    )) {
      if (matchesFolderPage(response.content, match)) {
        return true;
      }
      const children = response.content.nodes.filter((node) =>
        shouldScanChildFolder(node, match),
      );
      for (const node of children) {
        folders.add(node.id);
      }
    }
  }
  return false;
}
