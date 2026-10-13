import { reportClientError } from "@shared/utils/clientDiagnostics";
import { useCallback, useState } from "react";
import type { NodeContentDto } from "../../../shared/api/nodesApi";
import { batchItemsApi, toBatchItem } from "../../../shared/api/batchItemsApi";
import type { ConfirmResult, ConfirmOptions } from "material-ui-confirm";
import type { TFunction } from "i18next";
import type { FileSystemTile } from "@shared/types/FileListViewTypes";
import type { FileSelectionState } from "@shared/hooks/useFileSelection";
import { destructiveConfirmOptions } from "@shared/ui/confirmOptions";
import { taskManager } from "@shared/tasks";

type ConfirmFn = (options?: ConfirmOptions) => Promise<ConfirmResult>;

type TrashDeleteTarget = {
  kind: "folder" | "file";
  id: string;
  diagnosticsLabel: string;
};

const isConfirmed = (result: ConfirmResult): boolean => result.confirmed;

const toTargetKey = (target: TrashDeleteTarget): string =>
  target.kind + ":" + target.id;

const dedupeTargets = (targets: TrashDeleteTarget[]): TrashDeleteTarget[] => {
  const seen = new Set<string>();
  const deduped: TrashDeleteTarget[] = [];

  for (const target of targets) {
    const key = toTargetKey(target);
    if (seen.has(key)) {
      continue;
    }

    seen.add(key);
    deduped.push(target);
  }

  return deduped;
};

const collectTrashWrapperIds = (content: NodeContentDto): string[] => {
  const wrapperIds = new Set<string>();
  for (const node of content.nodes ?? []) {
    if (node.parentId) {
      wrapperIds.add(node.parentId);
    }
  }
  for (const file of content.files ?? []) {
    if (file.nodeId) {
      wrapperIds.add(file.nodeId);
    }
  }
  return [...wrapperIds];
};

const buildEmptyTrashTargets = (
  content: NodeContentDto,
  isTrashRoot: boolean,
): TrashDeleteTarget[] => {
  if (isTrashRoot) {
    return collectTrashWrapperIds(content).map((id) => ({
      kind: "folder",
      id,
      diagnosticsLabel: "trash wrapper",
    }));
  }

  return [
    ...(content.nodes ?? []).map((node) => ({
      kind: "folder" as const,
      id: node.id,
      diagnosticsLabel: "folder",
    })),
    ...(content.files ?? []).map((file) => ({
      kind: "file" as const,
      id: file.id,
      diagnosticsLabel: "file",
    })),
  ];
};

const buildSelectedTrashTargets = (
  selectedTiles: FileSystemTile[],
  resolveWrapperNodeId: (itemId: string) => string | null,
): TrashDeleteTarget[] => {
  const targets = selectedTiles.map((tile): TrashDeleteTarget => {
    const itemId = tile.kind === "folder" ? tile.node.id : tile.file.id;
    const wrapperId = resolveWrapperNodeId(itemId);

    if (wrapperId) {
      return {
        kind: "folder",
        id: wrapperId,
        diagnosticsLabel: "trash wrapper",
      };
    }

    return { kind: tile.kind, id: itemId, diagnosticsLabel: tile.kind };
  });

  return dedupeTargets(targets);
};

const runTrashDeleteTask = async (args: {
  targets: TrashDeleteTarget[];
  label: string;
  scopeLabel: string;
  failureMessage: string;
}): Promise<void> => {
  const { targets, label, scopeLabel, failureMessage } = args;
  const task = taskManager.createTask({
    kind: "delete",
    label,
    scopeLabel,
    bytesTotal: targets.length,
  });

  task.update({ status: "running" });

  try {
    const results = await batchItemsApi.delete(
      targets.map((target) => toBatchItem(target.id, target.kind)),
      true,
    );
    const deleted = targets.filter((target, index) => {
      const result = results[index];
      if (result?.id === target.id && result.deleted) {
        return true;
      }

      reportClientError(
        "Failed to delete " + target.diagnosticsLabel + " " + target.id,
      );
      return false;
    }).length;
    task.update({ bytesCompleted: deleted });
    if (deleted === targets.length) {
      task.complete();
      return;
    }
  } catch (error) {
    reportClientError("Failed to delete trash items", error);
  }

  task.update({ bytesCompleted: targets.length, progress01: 1 });
  task.fail({ message: failureMessage });
};

type UseTrashBulkActionsParams = {
  t: TFunction<["trash", "common", "files", "tasks"], undefined>;
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
  const [deletingTrash, setDeletingTrash] = useState(false);

  const startDeleteTask = useCallback(
    (args: {
      targets: TrashDeleteTarget[];
      label: string;
      onStarted?: () => void;
    }) => {
      setDeletingTrash(true);
      args.onStarted?.();

      void (async () => {
        try {
          await runTrashDeleteTask({
            targets: args.targets,
            label: args.label,
            scopeLabel: t("breadcrumbs.root"),
            failureMessage: t("errors.deleteFailed", { ns: "tasks" }),
          });
          await refreshContent();
        } finally {
          setDeletingTrash(false);
        }
      })();
    },
    [refreshContent, t],
  );

  const handleEmptyTrash = useCallback(async () => {
    if (!content || deletingTrash) {
      return;
    }

    const targets = buildEmptyTrashTargets(content, isTrashRoot);
    if (targets.length === 0) {
      return;
    }

    try {
      const result = await confirm({
        title: t("emptyTrash.confirmTitle"),
        description: t("emptyTrash.confirmDescription"),
        confirmationText: t("common:actions.delete"),
        cancellationText: t("common:actions.cancel"),
        ...destructiveConfirmOptions,
      });

      if (!isConfirmed(result)) {
        return;
      }

      startDeleteTask({
        targets,
        label: t("actions.emptyTrash"),
      });
    } catch {
      // Confirmation dialogs reject when dismissed. No deletion was started.
    }
  }, [confirm, content, deletingTrash, isTrashRoot, startDeleteTask, t]);

  const handleDeleteSelected = useCallback(async () => {
    if (
      !nodeId ||
      deletingTrash ||
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

    const targets = buildSelectedTrashTargets(
      selectedTiles,
      resolveWrapperNodeId,
    );
    if (targets.length === 0) {
      return;
    }

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
      ...destructiveConfirmOptions,
    });

    if (!isConfirmed(result)) {
      return;
    }

    startDeleteTask({
      targets,
      label: t("selection.deleteSelected", { ns: "files" }),
      onStarted: fileSelection.deselectAll,
    });
  }, [
    confirm,
    deletingTrash,
    fileSelection,
    nodeId,
    resolveWrapperNodeId,
    startDeleteTask,
    t,
    tiles,
  ]);

  return {
    deletingTrash,
    handleEmptyTrash,
    handleDeleteSelected,
  };
};
