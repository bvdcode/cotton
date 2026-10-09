import { reportClientError } from "@shared/utils/clientDiagnostics";
import * as React from "react";
import { batchItemsApi, toBatchItem } from "../../../shared/api/batchItemsApi";
import type { FileSystemTile } from "@shared/types/FileListViewTypes";
import type { FileSelectionState } from "@shared/hooks/useFileSelection";
import { destructiveConfirmOptions } from "@shared/ui/confirmOptions";

type TranslationFn = (
  key: string,
  options?: {
    ns?: string;
    count?: number;
  },
) => string;

type ConfirmFn = (
  args: {
    title: string;
    description: string;
    confirmationText: string;
    cancellationText: string;
  } & typeof destructiveConfirmOptions,
) => Promise<{ confirmed: boolean }>;

interface UseDeleteSelectedItemsArgs {
  nodeId: string | null;
  fileSelection: FileSelectionState;
  tiles: FileSystemTile[];
  confirm: ConfirmFn;
  t: TranslationFn;
  optimisticDeleteFile: (nodeId: string, fileId: string) => void;
  reloadCurrentNode: () => void;
  showToast: (message: string, variant?: "info" | "error") => void;
}

export const useDeleteSelectedItems = ({
  nodeId,
  fileSelection,
  tiles,
  confirm,
  t,
  optimisticDeleteFile,
  reloadCurrentNode,
  showToast,
}: UseDeleteSelectedItemsArgs) => {
  return React.useCallback(async () => {
    if (
      !nodeId ||
      !fileSelection.selectionMode ||
      fileSelection.selectedCount <= 0
    ) {
      return;
    }

    const selected = fileSelection.selectedIds;
    const selectedTiles = tiles.filter((tile) => {
      const id = tile.kind === "folder" ? tile.node.id : tile.file.id;
      return selected.has(id);
    });

    if (selectedTiles.length === 0) {
      return;
    }

    const result = await confirm({
      title: t("deleteSelected.confirmTitle", {
        ns: "files",
        count: selectedTiles.length,
      }),
      description: t("deleteSelected.confirmDescription", { ns: "files" }),
      confirmationText: t("common:actions.delete"),
      cancellationText: t("common:actions.cancel"),
      ...destructiveConfirmOptions,
    });

    if (!result.confirmed) {
      return;
    }

    try {
      const results = await batchItemsApi.delete(
        selectedTiles.map((tile) =>
          toBatchItem(
            tile.kind === "folder" ? tile.node.id : tile.file.id,
            tile.kind,
          ),
        ),
        false,
      );
      for (const [index, tile] of selectedTiles.entries()) {
        const id = tile.kind === "folder" ? tile.node.id : tile.file.id;
        if (results[index]?.id !== id || !results[index].deleted) {
          reportClientError("Failed to delete selected item", id);
          showToast(t("errors.deleteFailed", { ns: "tasks" }), "error");
          continue;
        }

        if (tile.kind === "file") {
          optimisticDeleteFile(nodeId, tile.file.id);
        }
      }
    } catch (error) {
      reportClientError("Failed to delete selected items", error);
      showToast(t("errors.deleteFailed", { ns: "tasks" }), "error");
    }

    fileSelection.deselectAll();

    reloadCurrentNode();
  }, [
    confirm,
    fileSelection,
    nodeId,
    optimisticDeleteFile,
    reloadCurrentNode,
    showToast,
    t,
    tiles,
  ]);
};
