import { useMemo } from "react";
import type { NodeContentDto } from "../api/nodesApi";
import type { FileSystemTile } from "../../pages/files/types/FileListViewTypes";

/**
 * Sort nodes/files alphabetically
 */
const sortByName = <T extends { name: string }>(items: T[]): T[] => {
  const sorted = items.slice();
  sorted.sort((a, b) =>
    a.name.localeCompare(b.name, undefined, { numeric: true }),
  );
  return sorted;
};

/**
 * Hook to build sorted folders, files and tiles from content
 */
export const useContentTiles = (content: NodeContentDto | undefined) => {
  const sortedFolders = useMemo(() => {
    return sortByName(content?.nodes ?? []);
  }, [content?.nodes]);

  const sortedFiles = useMemo(() => {
    return sortByName(content?.files ?? []);
  }, [content?.files]);

  const tiles = useMemo<FileSystemTile[]>(() => {
    return [
      ...sortedFolders.map((node) => ({ kind: "folder", node }) as const),
      ...sortedFiles.map((file) => ({ kind: "file", file }) as const),
    ];
  }, [sortedFolders, sortedFiles]);

  return { sortedFolders, sortedFiles, tiles };
};
