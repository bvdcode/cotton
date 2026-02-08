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

    // filePaths keys are node-file association IDs, not file manifest IDs.
    // Build a reverse map: fileName → [fullPath, ...] to match by name.
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
        const fullPath = consumeNextPath(file.name);
        // containerPath stays as the full original path for backend resolution
        const containerPath = fullPath ? getContainerPath(fullPath) : undefined;
        const displayContainerPath = containerPath
          ? stripRootPrefix(containerPath)
          : undefined;
        return {
          kind: "file",
          file,
          path: displayContainerPath,
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
