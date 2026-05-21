import { useCallback, useState } from "react";
import { nodesApi, type NodeContentDto } from "../../../shared/api/nodesApi";
import { filesApi } from "../../../shared/api/filesApi";
import type { ConfirmResult, ConfirmOptions } from "material-ui-confirm";
import type { TFunction } from "i18next";
import type { FileSystemTile } from "@shared/types/FileListViewTypes";
import type { FileSelectionState } from "@shared/hooks/useFileSelection";

type ConfirmFn = (options?: ConfirmOptions) => Promise<ConfirmResult>;

const isConfirmed = (result: ConfirmResult): boolean => result.confirmed;

const collectTrashWrapperIds = (content: NodeContentDto): string[] => {
  const wrapperIds = new Set<string>();
  for (const node of content.nodes ?? []) {
    if (node.parentId) wrapperIds.add(node.parentId);
  }
  for (const file of content.files ?? []) {
    if (file.nodeId) wrapperIds.add(file.nodeId);
  }
  return [...wrapperIds];
};

async function deleteNodeWithProgress(args: {
  nodeId: string;
  label: string;
  deleted: number;
  total: number;
  onProgress: (current: number, total: number) => void;
}): Promise<number> {
  const { nodeId, label, deleted, total, onProgress } = args;
  try {
    await nodesApi.deleteNode(nodeId, true);
    const nextDeleted = deleted + 1;
    onProgress(nextDeleted, total);
    return nextDeleted;
  } catch (error) {
    console.error("Failed to delete " + label + " " + nodeId + ":", error);
    return deleted;
  }
}

async function deleteFileWithProgress(args: {
  fileId: string;
  deleted: number;
  total: number;
  onProgress: (current: number, total: number) => void;
}): Promise<number> {
  const { fileId, deleted, total, onProgress } = args;
  try {
    await filesApi.deleteFile(fileId, true);
    const nextDeleted = deleted + 1;
    onProgress(nextDeleted, total);
    return nextDeleted;
  } catch (error) {
    console.error("Failed to delete file " + fileId + ":", error);
    return deleted;
  }
}

async function deleteTrashRootWrappers(
  content: NodeContentDto,
  onProgress: (current: number, total: number) => void,
): Promise<void> {
  const wrappers = collectTrashWrapperIds(content);
  let deleted = 0;
  for (const wrapperId of wrappers) {
    deleted = await deleteNodeWithProgress({
      nodeId: wrapperId,
      label: "trash wrapper",
      deleted,
      total: wrappers.length,
      onProgress,
    });
  }
}

async function deleteTrashFolderContents(
  content: NodeContentDto,
  onProgress: (current: number, total: number) => void,
): Promise<void> {
  const total = (content.nodes?.length ?? 0) + (content.files?.length ?? 0);
  let deleted = 0;

  for (const folder of content.nodes ?? []) {
    deleted = await deleteNodeWithProgress({
      nodeId: folder.id,
      label: "folder",
      deleted,
      total,
      onProgress,
    });
  }

  for (const file of content.files ?? []) {
    deleted = await deleteFileWithProgress({
      fileId: file.id,
      deleted,
      total,
      onProgress,
    });
  }
}

async function deleteAllTrashItems(args: {
  content: NodeContentDto;
  isTrashRoot: boolean;
  onProgress: (current: number, total: number) => void;
}): Promise<void> {
  const { content, isTrashRoot, onProgress } = args;
  if (isTrashRoot) {
    await deleteTrashRootWrappers(content, onProgress);
    return;
  }

  await deleteTrashFolderContents(content, onProgress);
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
