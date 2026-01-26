import { useMemo } from "react";
import type { FileSystemTile } from "../../../pages/files/types/FileListViewTypes";
import type { FileListSource } from "../../types/fileListSource";
import type { NodeDto } from "../../api/layoutsApi";
import type { NodeFileManifestDto } from "../../api/nodesApi";

interface SearchResults {
  nodes?: NodeDto[];
  files?: NodeFileManifestDto[];
}

interface UseSearchFileListOptions {
  results: SearchResults | null;
  loading: boolean;
  error: string | null;
  totalCount: number;
  hasQuery: boolean;
}

export const useSearchFileList = ({
  results,
  loading,
  error,
  totalCount,
  hasQuery,
}: UseSearchFileListOptions): FileListSource => {
  const tiles: FileSystemTile[] = useMemo(() => {
    if (!results || !hasQuery) return [];

    const sortByName = <T extends { name: string }>(items: T[]): T[] => {
      const sorted = items.slice();
      sorted.sort((a, b) =>
        a.name.localeCompare(b.name, undefined, { numeric: true }),
      );
      return sorted;
    };

    const sortedFolders = sortByName(results.nodes ?? []);
    const sortedFiles = sortByName(results.files ?? []);

    return [
      ...sortedFolders.map((node) => ({ kind: "folder", node }) as const),
      ...sortedFiles.map((file) => ({ kind: "file", file }) as const),
    ];
  }, [results, hasQuery]);

  return {
    loading,
    error,
    tiles,
    totalCount,
  };
};
