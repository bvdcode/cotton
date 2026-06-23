import { useMemo } from "react";
import type { NodeDto } from "../api/layoutsApi";
import type {
  FileListFileDto,
  FileSystemTile,
} from "@shared/types/FileListViewTypes";

interface ContentTilesSource<TFile extends FileListFileDto> {
  nodes: NodeDto[];
  files: TFile[];
}

type ContentTilesSortMode = "name" | "updatedAtDesc";

interface ContentTilesOptions {
  sortMode?: ContentTilesSortMode;
}

const compareNames = (a: string, b: string): number =>
  a.localeCompare(b, undefined, { numeric: true });

const compareUpdatedAtDesc = (
  aUpdatedAt: string,
  bUpdatedAt: string,
  aName: string,
  bName: string,
): number => {
  const updatedAtDiff = Date.parse(bUpdatedAt) - Date.parse(aUpdatedAt);
  return updatedAtDiff === 0 ? compareNames(aName, bName) : updatedAtDiff;
};

const sortByName = <T extends { name: string }>(items: T[]): T[] => {
  const sorted = items.slice();
  sorted.sort((a, b) => compareNames(a.name, b.name));
  return sorted;
};

const sortByUpdatedAtDesc = <T extends { name: string; updatedAt: string }>(
  items: T[],
): T[] => {
  const sorted = items.slice();
  sorted.sort((a, b) =>
    compareUpdatedAtDesc(a.updatedAt, b.updatedAt, a.name, b.name),
  );
  return sorted;
};

const sortItems = <T extends { name: string; updatedAt: string }>(
  items: T[],
  sortMode: ContentTilesSortMode,
): T[] =>
  sortMode === "updatedAtDesc" ? sortByUpdatedAtDesc(items) : sortByName(items);

const getTileUpdatedAt = (tile: FileSystemTile): string =>
  tile.kind === "folder" ? tile.node.updatedAt : tile.file.updatedAt;

const getTileName = (tile: FileSystemTile): string =>
  tile.kind === "folder" ? tile.node.name : tile.file.name;

const sortTilesByUpdatedAtDesc = (
  tiles: FileSystemTile[],
): FileSystemTile[] => {
  const sorted = tiles.slice();
  sorted.sort((a, b) =>
    compareUpdatedAtDesc(
      getTileUpdatedAt(a),
      getTileUpdatedAt(b),
      getTileName(a),
      getTileName(b),
    ),
  );
  return sorted;
};

export const useContentTiles = <TFile extends FileListFileDto>(
  content: ContentTilesSource<TFile> | undefined,
  options: ContentTilesOptions = {},
) => {
  const sortMode = options.sortMode ?? "name";

  const sortedFolders = useMemo(() => {
    return sortItems(content?.nodes ?? [], sortMode);
  }, [content?.nodes, sortMode]);

  const sortedFiles = useMemo(() => {
    return sortItems(content?.files ?? [], sortMode);
  }, [content?.files, sortMode]);

  const tiles = useMemo<FileSystemTile[]>(() => {
    const groupedTiles: FileSystemTile[] = [
      ...sortedFolders.map((node) => ({ kind: "folder", node }) as const),
      ...sortedFiles.map((file) => ({ kind: "file", file }) as const),
    ];

    return sortMode === "updatedAtDesc"
      ? sortTilesByUpdatedAtDesc(groupedTiles)
      : groupedTiles;
  }, [sortMode, sortedFolders, sortedFiles]);

  return { sortedFolders, sortedFiles, tiles };
};
