import { useMemo } from "react";
import type { FileSystemTile } from "../../../shared/types/FileListViewTypes";
import type { SearchRow } from "../types";

export const useSearchContentRows = (tiles: FileSystemTile[]): SearchRow[] =>
  useMemo(() => {
    const rows: SearchRow[] = [];
    for (const tile of tiles) {
      if (tile.kind === "folder") {
        rows.push({
          id: `folder-${tile.node.id}`,
          kind: "folder",
          node: tile.node,
          path: tile.path,
        });
        continue;
      }
      if ("ownerId" in tile.file && "metadata" in tile.file) {
        rows.push({
          id: `file-${tile.file.id}`,
          kind: "file",
          file: tile.file,
          path: tile.path,
        });
      }
    }
    return rows;
  }, [tiles]);
