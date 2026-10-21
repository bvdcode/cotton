import type { NodeContentDto, NodeFileManifestDto } from "../api/nodesApi";
import { hasEncryptedFolderName } from "../crypto/folderDisplayMeta";
import { isFileEncrypted } from "../crypto/metadataFlags";
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

export async function findFolderEncryptionMatch(
  nodeId: string,
  match: FolderEncryptionMatch,
  signal: AbortSignal,
  firstPage?: FolderScanPage,
): Promise<boolean> {
  const recursive = match !== "encrypted-names";
  const pending = [nodeId];
  const visited = new Set(pending);
  while (pending.length > 0) {
    const current = pending.shift();
    if (!current) {
      break;
    }
    for await (const response of readFolderPages(
      current,
      signal,
      current === nodeId ? firstPage : undefined,
    )) {
      if (matchesFolderPage(response.content, match)) {
        return true;
      }
      if (recursive) {
        for (const node of response.content.nodes) {
          if (!visited.has(node.id)) {
            visited.add(node.id);
            pending.push(node.id);
          }
        }
      }
    }
  }
  return false;
}
