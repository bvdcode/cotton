import { useMemo } from "react";
import { useFileInteractionHandlers } from "@shared/hooks/useFileInteractionHandlers";
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
  hasContent: boolean;
  sortedFiles: NodeFileManifestDto[];
  interaction: ReturnType<typeof useFileInteractionHandlers>;
  capabilities: FileListCapabilities;
}

export type FileListSourceLogic = Omit<FileListPageLogic, "interaction">;

const isNodeFileManifest = (
  file: FileListFileDto,
): file is NodeFileManifestDto => "id" in file && "name" in file;

export const useFileListSourceLogic = ({
  source,
  sourceKind,
}: UseFileListPageLogicOptions): FileListSourceLogic => {
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

  const capabilities = useMemo(
    () => getFileListCapabilities(sourceKind),
    [sourceKind],
  );

  return {
    tiles: source.tiles,
    loading: source.loading,
    error: source.error,
    totalCount: source.totalCount,
    isContentTransitioning: source.isContentTransitioning,
    hasContent: source.hasContent,
    sortedFiles,
    capabilities,
  };
};

export const useFileListPageLogic = (
  options: UseFileListPageLogicOptions,
): FileListPageLogic => {
  const sourceLogic = useFileListSourceLogic(options);
  const interaction = useFileInteractionHandlers({
    sortedFiles: sourceLogic.sortedFiles,
  });

  return {
    ...sourceLogic,
    interaction,
  };
};
