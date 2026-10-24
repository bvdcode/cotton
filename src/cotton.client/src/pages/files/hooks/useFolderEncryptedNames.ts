import { useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import type { NodeContentDto } from "@shared/api/nodesApi";
import { queryKeys } from "@shared/api/queries/queryKeys";
import { hasEncryptedFolderName } from "@shared/crypto/folderDisplayMeta";
import { isFileEncrypted } from "@shared/crypto/metadataFlags";
import {
  readFolderPages,
  type FolderScanPage,
} from "@shared/utils/folderPages";
import { reportClientError } from "@shared/utils/clientDiagnostics";

function hasEncryptedNames(content: NodeContentDto): boolean {
  return (
    content.files.some((file) => isFileEncrypted(file.metadata)) ||
    content.nodes.some((node) => hasEncryptedFolderName(node.metadata))
  );
}

export function useFolderEncryptedNames(options: {
  nodeId: string | null;
  enabled: boolean;
  firstPage?: FolderScanPage;
}): boolean | undefined {
  const { nodeId, enabled, firstPage } = options;
  const content = firstPage?.response.content;
  const found = Boolean(content && hasEncryptedNames(content));
  const complete = Boolean(
    firstPage &&
    content &&
    content.files.length + content.nodes.length >=
      firstPage.response.totalCount,
  );
  const query = useQuery({
    queryKey: [
      ...queryKeys.nodeChildren.all(nodeId ?? ""),
      "encryptionPresence",
      "encrypted-names",
    ],
    queryFn: async ({ signal }) => {
      if (!nodeId) {
        throw new Error("Folder id is required");
      }
      for await (const response of readFolderPages(nodeId, signal, firstPage)) {
        if (hasEncryptedNames(response.content)) {
          return true;
        }
      }
      return false;
    },
    enabled: enabled && nodeId !== null && !found && !complete,
  });
  useEffect(() => {
    if (query.error) {
      reportClientError("Failed to check folder names", query.error);
    }
  }, [query.error]);
  if (!enabled) {
    return undefined;
  }
  if (found) {
    return true;
  }
  if (complete) {
    return false;
  }
  return query.data;
}
