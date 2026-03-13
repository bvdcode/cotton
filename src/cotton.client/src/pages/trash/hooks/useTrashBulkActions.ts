import { useCallback, useState } from "react";
import { nodesApi, type NodeContentDto } from "../../../shared/api/nodesApi";
import { filesApi } from "../../../shared/api/filesApi";
import type { ConfirmResult, ConfirmOptions } from "material-ui-confirm";
import type { TFunction } from "i18next";
import type { FileSystemTile } from "../../files/types/FileListViewTypes";
import type { FileSelectionState } from "../../files/hooks/useFileSelection";

type ConfirmFn = (options?: ConfirmOptions) => Promise<ConfirmResult>;

const isConfirmed = (result: ConfirmResult): boolean => result.confirmed;

async function deleteAllTrashItems(args: {
  content: NodeContentDto;
  isTrashRoot: boolean;
  onProgress: (current: number, total: number) => void;
}): Promise<void> {
  const { content, isTrashRoot, onProgress } = args;

  if (isTrashRoot) {
    const wrapperIds = new Set<string>();
    for (const node of content.nodes ?? []) {
      if (node.parentId) wrapperIds.add(node.parentId);
    }
    for (const file of content.files ?? []) {
      if (file.nodeId) wrapperIds.add(file.nodeId);
    }

    const wrappers = [...wrapperIds];
    let deleted = 0;

    for (const wrapperId of wrappers) {
      try {
        await nodesApi.deleteNode(wrapperId, true);
        deleted += 1;
        onProgress(deleted, wrappers.length);
      } catch (error) {
        console.error(`Failed to delete trash wrapper ${wrapperId}:`, error);
      }
    }

    return;
  }

  const totalItems = (content.nodes?.length ?? 0) + (content.files?.length ?? 0);
  let deleted = 0;

  for (const folder of content.nodes ?? []) {
    try {
      await nodesApi.deleteNode(folder.id, true);
      deleted += 1;
      onProgress(deleted, totalItems);
    } catch (error) {
      console.error(`Failed to delete folder ${folder.id}:`, error);
    }
  }

  for (const file of content.files ?? []) {
    try {
      await filesApi.deleteFile(file.id, true);
      deleted += 1;
      onProgress(deleted, totalItems);
    } catch (error) {
      console.error(`Failed to delete file ${file.id}:`, error);
    }
  }
}

type UseTrashBulkActionsParams = {
  t: TFunction<["trash", "common", "files"], undefined>;
  confirm: ConfirmFn;
  content: NodeContentDto | undefined;
  tiles: FileSystemTile[];
  nodeId: string | null;
  isTrashRoot: boolean;
  fileSelection: FileSelectionState;
  resolveWrapperNodeId: (itemId: string) => string | null;
  refreshContent: () => Promise<void>;
};

export const useTrashBulkActions = ({
  t,
  confirm,
  content,
  tiles,
  nodeId,
  isTrashRoot,
  fileSelection,
  resolveWrapperNodeId,
  refreshContent,
}: UseTrashBulkActionsParams) => {
  const [emptyingTrash, setEmptyingTrash] = useState(false);
  const [emptyTrashProgress, setEmptyTrashProgress] = useState({
    current: 0,
    total: 0,
  });

  const handleEmptyTrash = useCallback(async () => {
    if (!content) return;

    const totalItems = (content.nodes?.length ?? 0) + (content.files?.length ?? 0);
    if (totalItems === 0) return;

    try {
      const result = await confirm({
        title: t("emptyTrash.confirmTitle"),
        description: t("emptyTrash.confirmDescription"),
        confirmationText: t("common:actions.delete"),
        cancellationText: t("common:actions.cancel"),
        confirmationButtonProps: { color: "error" },
      });

      if (!isConfirmed(result)) return;

      setEmptyingTrash(true);
      setEmptyTrashProgress({ current: 0, total: totalItems });

      await deleteAllTrashItems({
        content,
        isTrashRoot,
        onProgress: (current, total) => setEmptyTrashProgress({ current, total }),
      });

      setEmptyingTrash(false);
      await refreshContent();
    } catch {
      setEmptyingTrash(false);
    }
  }, [confirm, content, isTrashRoot, refreshContent, t]);

  const handleDeleteSelected = useCallback(async () => {
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
      title: t("deleteSelectedForever.confirmTitle", {
        ns: "trash",
        count: selectedTiles.length,
      }),
      description: t("deleteSelectedForever.confirmDescription", {
        ns: "trash",
      }),
      confirmationText: t("common:actions.delete"),
      cancellationText: t("common:actions.cancel"),
      confirmationButtonProps: { color: "error" },
    });

    if (!isConfirmed(result)) return;

    let hadError = false;

    for (const tile of selectedTiles) {
      try {
        const id = tile.kind === "folder" ? tile.node.id : tile.file.id;
        const wrapperId = resolveWrapperNodeId(id);

        if (wrapperId) {
          await nodesApi.deleteNode(wrapperId, true);
          continue;
        }

        if (tile.kind === "folder") {
          await nodesApi.deleteNode(tile.node.id, true);
        } else {
          await filesApi.deleteFile(tile.file.id, true);
        }
      } catch (error) {
        hadError = true;
        console.error("Failed to delete selected trash item", error);
      }
    }

    fileSelection.deselectAll();
    await refreshContent();

    if (hadError) {
      // Keep console diagnostics; UI refresh already triggered.
    }
  }, [confirm, fileSelection, nodeId, refreshContent, resolveWrapperNodeId, t, tiles]);

  return {
    emptyingTrash,
    emptyTrashProgress,
    handleEmptyTrash,
    handleDeleteSelected,
  };
};
