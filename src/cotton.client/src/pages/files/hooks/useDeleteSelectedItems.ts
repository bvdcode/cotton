import * as React from "react";
import { filesApi } from "../../../shared/api/filesApi";
import type { FileSystemTile } from "../types/FileListViewTypes";
import type { FileSelectionState } from "./useFileSelection";

type TranslationFn = (
  key: string,
  options?: {
    ns?: string;
    count?: number;
  },
) => string;

type ConfirmFn = (args: {
  title: string;
  description: string;
  confirmationText: string;
  cancellationText: string;
  confirmationButtonProps: { color: "error" };
}) => Promise<{ confirmed: boolean }>;

interface UseDeleteSelectedItemsArgs {
  nodeId: string | null;
  fileSelection: FileSelectionState;
  tiles: FileSystemTile[];
  confirm: ConfirmFn;
  t: TranslationFn;
  deleteFolder: (folderId: string, parentId?: string) => Promise<boolean>;
  optimisticDeleteFile: (nodeId: string, fileId: string) => void;
  reloadCurrentNode: () => void;
}

export const useDeleteSelectedItems = ({
  nodeId,
  fileSelection,
  tiles,
  confirm,
  t,
  deleteFolder,
  optimisticDeleteFile,
  reloadCurrentNode,
}: UseDeleteSelectedItemsArgs) => {
  return React.useCallback(async () => {
    if (!nodeId) return;
    if (!fileSelection.selectionMode) return;
    if (fileSelection.selectedCount <= 0) return;

    const selected = fileSelection.selectedIds;
    const selectedTiles = tiles.filter((tile) => {
      const id = tile.kind === "folder" ? tile.node.id : tile.file.id;
      return selected.has(id);
    });

    if (selectedTiles.length === 0) return;

    const result = await confirm({
      title: t("deleteSelected.confirmTitle", {
        ns: "files",
        count: selectedTiles.length,
      }),
      description: t("deleteSelected.confirmDescription", { ns: "files" }),
      confirmationText: t("common:actions.delete"),
      cancellationText: t("common:actions.cancel"),
      confirmationButtonProps: { color: "error" },
    });

    if (!result.confirmed) return;

    let hadError = false;

    for (const tile of selectedTiles) {
      if (tile.kind === "folder") {
        try {
          await deleteFolder(tile.node.id, nodeId);
        } catch (error) {
          hadError = true;
          console.error("Failed to delete selected folder", error);
        }
        continue;
      }

      try {
        optimisticDeleteFile(nodeId, tile.file.id);
        await filesApi.deleteFile(tile.file.id);
      } catch (error) {
        hadError = true;
        console.error("Failed to delete selected file", error);
      }
    }

    fileSelection.deselectAll();

    if (hadError) {
      reloadCurrentNode();
      return;
    }

    reloadCurrentNode();
  }, [
    confirm,
    deleteFolder,
    fileSelection,
    nodeId,
    optimisticDeleteFile,
    reloadCurrentNode,
    t,
    tiles,
  ]);
};
