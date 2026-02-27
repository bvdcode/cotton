import { useMemo } from "react";
import type { FileSystemTile } from "../../../pages/files/types/FileListViewTypes";
import type { FileListSource } from "../../types/fileListSource";
import type { NodeDto } from "../../api/layoutsApi";
import type { NodeFileManifestDto } from "../../api/nodesApi";

interface SearchResults {
  nodes?: NodeDto[];
  files?: NodeFileManifestDto[];
  nodePaths?: Record<string, string>;
  filePaths?: Record<string, string>;
}

interface UseSearchFileListOptions {
  results: SearchResults | null;
  loading: boolean;
  error: string | null;
  totalCount: number;
  hasQuery: boolean;
  rootNodeName?: string | null;
}

export const useSearchFileList = ({
  results,
  loading,
  error,
  totalCount,
  hasQuery,
  rootNodeName,
}: UseSearchFileListOptions): FileListSource => {
  const tiles: FileSystemTile[] = useMemo(() => {
    if (!results || !hasQuery) return [];

    const nodePaths = results.nodePaths ?? {};
    const filePaths = results.filePaths ?? {};

    // Strip root node prefix from display paths (e.g. "/Default/Music" → "/Music")
    const rootPrefix = rootNodeName ? `/${rootNodeName}` : null;
    const stripRootPrefix = (path: string): string => {
      if (rootPrefix && path.startsWith(rootPrefix)) {
        const stripped = path.slice(rootPrefix.length);
        return stripped.length === 0 ? "/" : stripped;
      }
      return path;
    };

    const getContainerPath = (fullPath: string): string => {
      const normalized = fullPath.trim();
      const parts = normalized.split("/").filter((p) => p.length > 0);
      if (parts.length <= 1) return "/";
      return `/${parts.slice(0, -1).join("/")}`;
    };

    const sortByName = <T extends { name: string }>(items: T[]): T[] => {
      const sorted = items.slice();
      sorted.sort((a, b) =>
        a.name.localeCompare(b.name, undefined, { numeric: true }),
      );
      return sorted;
    };

    // Some backend versions return filePaths keyed by file manifest IDs.
    // Others may return node-file association IDs. Prefer direct lookup by file.id,
    // but keep a best-effort fallback to match by file name.
    const filePathsByName = new Map<string, string[]>();
    for (const fullPath of Object.values(filePaths)) {
      const segments = fullPath.split("/");
      const fileName = segments[segments.length - 1];
      const list = filePathsByName.get(fileName) ?? [];
      list.push(fullPath);
      filePathsByName.set(fileName, list);
    }

    const consumedIndices = new Map<string, number>();
    const consumeNextPath = (fileName: string): string | undefined => {
      const paths = filePathsByName.get(fileName);
      if (!paths) return undefined;
      const idx = consumedIndices.get(fileName) ?? 0;
      if (idx >= paths.length) return undefined;
      consumedIndices.set(fileName, idx + 1);
      return paths[idx];
    };

    const getFullPathForFile = (file: NodeFileManifestDto): string | undefined => {
      const byId = filePaths[file.id];
      if (byId) return byId;
      return consumeNextPath(file.name);
    };

    const sortedFolders = sortByName(results.nodes ?? []);
    const sortedFiles = sortByName(results.files ?? []);

    return [
      ...sortedFolders.map(
        (node) =>
          ({
            kind: "folder",
            node,
            path: nodePaths[node.id]
              ? stripRootPrefix(nodePaths[node.id])
              : undefined,
          }) as const,
      ),
      ...sortedFiles.map((file) => {
        const fullPath = getFullPathForFile(file);

        // containerPath stays as the full original path for backend resolution
        const containerPath = fullPath ? getContainerPath(fullPath) : undefined;

        // Display the full file path (including file name) as returned by backend.
        const displayFullPath = fullPath ? stripRootPrefix(fullPath) : undefined;
        return {
          kind: "file",
          file,
          path: displayFullPath,
          containerPath,
        } as const;
      }),
    ];
  }, [results, hasQuery, rootNodeName]);

  return {
    loading,
    error,
    tiles,
    totalCount,
  };
};
