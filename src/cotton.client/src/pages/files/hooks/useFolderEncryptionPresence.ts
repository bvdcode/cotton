import { useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import type { NodeFileManifestDto } from "../../../shared/api/nodesApi";
import { queryKeys } from "../../../shared/api/queries/queryKeys";
import { type FolderScanPage } from "../../../shared/utils/folderPages";
import {
  findFolderEncryptionMatch,
  matchesFileEncryption,
  matchesFolderPage,
  type FolderEncryptionMatch,
} from "../../../shared/utils/folderEncryptionPresence";
import { reportClientError } from "../../../shared/utils/clientDiagnostics";

export function useFolderEncryptionPresence(options: {
  nodeId: string | null;
  match: FolderEncryptionMatch;
  enabled: boolean;
  firstPage?: FolderScanPage;
  knownFiles?: readonly NodeFileManifestDto[];
}): boolean | undefined {
  const { nodeId, match, enabled, firstPage, knownFiles } = options;
  const content = firstPage?.response.content;
  const found =
    Boolean(content && matchesFolderPage(content, match)) ||
    Boolean(knownFiles?.some((file) => matchesFileEncryption(file, match)));
  const complete = Boolean(
    firstPage &&
    content &&
    content.files.length + content.nodes.length >=
      firstPage.response.totalCount &&
    (match === "encrypted-names" || content.nodes.length === 0),
  );
  const query = useQuery({
    queryKey: [
      ...queryKeys.nodeChildren.all(nodeId ?? ""),
      "encryptionPresence",
      match,
    ],
    queryFn: ({ signal }) => {
      if (!nodeId) {
        throw new Error("Folder id is required");
      }
      return findFolderEncryptionMatch(nodeId, match, signal, firstPage);
    },
    enabled: enabled && nodeId !== null && !found && !complete,
  });
  useEffect(() => {
    if (query.error) {
      reportClientError("Failed to check folder encryption", query.error);
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
