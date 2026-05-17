import { useMemo } from "react";
import { useFileInteractionHandlers } from "./useFileInteractionHandlers";
import type {
  FileListFileDto,
  FileSystemTile,
} from "../../../shared/types/FileListViewTypes";
import type { NodeFileManifestDto } from "../../../shared/api/nodesApi";
import type { FileListSource } from "../../../shared/types/fileListSource";
import {
  type FileListCapabilities,
  type FileListSourceKind,
  getFileListCapabilities,
} from "../../../shared/types/fileListSourceKind";

export interface UseFileListPageLogicOptions {
  source: FileListSource;
  sourceKind: FileListSourceKind;
}

export interface FileListPageLogic {
  tiles: FileSystemTile[];
  loading: boolean;
  error: string | null;
  totalCount?: number;
  isContentTransitioning: boolean;
  sortedFiles: NodeFileManifestDto[];
  interaction: ReturnType<typeof useFileInteractionHandlers>;
  capabilities: FileListCapabilities;
}

const isNodeFileManifest = (
  file: FileListFileDto,
): file is NodeFileManifestDto => "id" in file && "name" in file;

export const useFileListPageLogic = ({
  source,
  sourceKind,
}: UseFileListPageLogicOptions): FileListPageLogic => {
  const sortedFiles = useMemo<NodeFileManifestDto[]>(() => {
    const files: NodeFileManifestDto[] = [];

    for (const tile of source.tiles) {
      if (tile.kind !== "file") continue;
      if (isNodeFileManifest(tile.file)) {
        files.push(tile.file);
      }
    }

    files.sort((a, b) =>
      a.name.localeCompare(b.name, undefined, { numeric: true }),
    );
    return files;
  }, [source.tiles]);

  const interaction = useFileInteractionHandlers({ sortedFiles });
  const capabilities = useMemo(
    () => getFileListCapabilities(sourceKind),
    [sourceKind],
  );

  return {
    tiles: source.tiles,
    loading: source.loading,
    error: source.error,
    totalCount: source.totalCount,
    isContentTransitioning: source.isContentTransitioning ?? false,
    sortedFiles,
    interaction,
    capabilities,
  };
};
