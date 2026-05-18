import { useCallback, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { filesApi } from "../../../shared/api/filesApi";
import {
  nodesApi,
  type RestoreConflictKind,
  type RestoreOutcomeDto,
} from "../../../shared/api/nodesApi";
import type { FileSelectionState } from "@shared/hooks/useFileSelection";
import type { FileSystemTile } from "@shared/types/FileListViewTypes";

const maxPromptHops = 3;
const originalParentPathMetadataKey = "originalParentPath";

export type RestorableItem = {
  id: string;
  kind: "folder" | "file";
  name: string;
  restorePath?: string | null;
};

type PromptKind =
  | { kind: "confirm"; restorePath: string }
  | { kind: "parentMissing"; missingPath: string }
  | { kind: "conflict"; conflictKind: RestoreConflictKind; conflictName: string };

type PromptState = {
  item: RestorableItem;
  prompt: PromptKind;
};

export type PromptDecision = {
  action: "apply" | "skip";
  applyToAll?: boolean;
};

export type RestoreProgress = {
  current: number;
  total: number;
  itemName: string;
};

type UseTrashRestoreActionsParams = {
  fileSelection: FileSelectionState;
  tiles: FileSystemTile[];
  refreshContent: () => Promise<void> | void;
};

export const useTrashRestoreActions = ({
  fileSelection,
  tiles,
  refreshContent,
}: UseTrashRestoreActionsParams) => {
  const { t } = useTranslation(["trash"]);

  const [activePrompt, setActivePrompt] = useState<PromptState | null>(null);
  const [restoring, setRestoring] = useState(false);
  const [progress, setProgress] = useState<RestoreProgress>({
    current: 0,
    total: 0,
    itemName: "",
  });
  const [errors, setErrors] = useState<string[]>([]);

  const stickyConfirm = useRef<PromptDecision["action"] | null>(null);
  const stickyParentMissing = useRef<PromptDecision["action"] | null>(null);
  const stickyConflict = useRef<PromptDecision["action"] | null>(null);
  const restoreInFlight = useRef(false);
  const resolvePromptRef = useRef<((decision: PromptDecision) => void) | null>(null);

  const requestDecision = useCallback(
    (item: RestorableItem, prompt: PromptKind): Promise<PromptDecision> =>
      new Promise((resolve) => {
        resolvePromptRef.current = resolve;
        setActivePrompt({ item, prompt });
      }),
    [],
  );

  const handlePromptAnswer = useCallback((decision: PromptDecision) => {
    const resolve = resolvePromptRef.current;
    resolvePromptRef.current = null;
    setActivePrompt(null);
    resolve?.(decision);
  }, []);

  const getRestorePath = useCallback(
    (item: RestorableItem): string => {
      if (item.restorePath !== undefined && item.restorePath !== null) {
        return item.restorePath;
      }

      const tile = tiles.find((candidate) => {
        if (item.kind === "folder" && candidate.kind === "folder") {
          return candidate.node.id === item.id;
        }

        if (item.kind === "file" && candidate.kind === "file") {
          return candidate.file.id === item.id;
        }

        return false;
      });

      if (!tile) {
        return "";
      }

      return tile.kind === "folder"
        ? (tile.node.metadata?.[originalParentPathMetadataKey] ?? "")
        : (tile.file.metadata?.[originalParentPathMetadataKey] ?? "");
    },
    [tiles],
  );

  const callRestore = useCallback(
    (item: RestorableItem, options: { createMissingParents: boolean; overwrite: boolean }) =>
      item.kind === "folder"
        ? nodesApi.restoreNode(item.id, options)
        : filesApi.restoreFile(item.id, options),
    [],
  );

  const restoreSingle = useCallback(
    async (item: RestorableItem): Promise<"restored" | "skipped" | "failed"> => {
      const restorePath = getRestorePath(item);

      if (stickyConfirm.current === "skip") {
        return "skipped";
      }

      if (stickyConfirm.current !== "apply") {
        const decision = await requestDecision(item, {
          kind: "confirm",
          restorePath,
        });
        if (decision.applyToAll) {
          stickyConfirm.current = decision.action;
        }
        if (decision.action === "skip") {
          return "skipped";
        }
      }

      let createMissingParents = false;
      let overwrite = false;

      for (let attempt = 0; attempt < maxPromptHops; attempt += 1) {
        let outcome: RestoreOutcomeDto;
        try {
          outcome = await callRestore(item, { createMissingParents, overwrite });
        } catch (error) {
          console.error("Restore call failed", error);
          setErrors((prev) => [...prev, t("restore.failed", { name: item.name })]);
          return "failed";
        }

        if (outcome.status === "Restored") {
          return "restored";
        }

        if (outcome.status === "NotRestorable") {
          const reason = outcome.reason ?? t("restore.notRestorable");
          setErrors((prev) => [...prev, `${item.name}: ${reason}`]);
          return "failed";
        }

        if (outcome.status === "ParentMissing") {
          if (stickyParentMissing.current === "skip") {
            return "skipped";
          }

          if (stickyParentMissing.current !== "apply") {
            const decision = await requestDecision(item, {
              kind: "parentMissing",
              missingPath: outcome.missingPath ?? "",
            });
            if (decision.applyToAll) {
              stickyParentMissing.current = decision.action;
            }
            if (decision.action === "skip") {
              return "skipped";
            }
          }

          createMissingParents = true;
          continue;
        }

        if (outcome.status === "Conflict") {
          if (stickyConflict.current === "skip") {
            return "skipped";
          }

          if (stickyConflict.current !== "apply") {
            const decision = await requestDecision(item, {
              kind: "conflict",
              conflictKind: outcome.conflictKind ?? "File",
              conflictName: outcome.conflictName ?? item.name,
            });
            if (decision.applyToAll) {
              stickyConflict.current = decision.action;
            }
            if (decision.action === "skip") {
              return "skipped";
            }
          }

          overwrite = true;
          continue;
        }

        return "failed";
      }

      setErrors((prev) => [...prev, t("restore.failed", { name: item.name })]);
      return "failed";
    },
    [callRestore, getRestorePath, requestDecision, t],
  );

  const restoreItems = useCallback(
    async (items: RestorableItem[]) => {
      if (items.length === 0 || restoreInFlight.current) {
        return;
      }

      restoreInFlight.current = true;
      stickyConfirm.current = null;
      stickyParentMissing.current = null;
      stickyConflict.current = null;
      setErrors([]);
      setRestoring(true);

      try {
        for (let i = 0; i < items.length; i += 1) {
          const item = items[i];
          setProgress({ current: i + 1, total: items.length, itemName: item.name });
          await restoreSingle(item);
        }
      } finally {
        restoreInFlight.current = false;
        setRestoring(false);
        setProgress({ current: items.length, total: items.length, itemName: "" });
        fileSelection.deselectAll();
        await refreshContent();
      }
    },
    [fileSelection, refreshContent, restoreSingle],
  );

  const restoreSelected = useCallback(async () => {
    const selected = new Set(fileSelection.selectedIds);
    if (selected.size === 0) {
      return;
    }

    const items = tiles
      .filter((tile) => {
        const id = tile.kind === "folder" ? tile.node.id : tile.file.id;
        return selected.has(id);
      })
      .map((tile): RestorableItem => (
        tile.kind === "folder"
          ? { id: tile.node.id, kind: "folder", name: tile.node.name }
          : { id: tile.file.id, kind: "file", name: tile.file.name }
      ));

    await restoreItems(items);
  }, [fileSelection.selectedIds, restoreItems, tiles]);

  const restoreItem = useCallback(
    async (item: RestorableItem) => {
      await restoreItems([item]);
    },
    [restoreItems],
  );

  return useMemo(
    () => ({
      restoring,
      progress,
      errors,
      activePrompt,
      handlePromptAnswer,
      restoreItem,
      restoreSelected,
      clearErrors: () => setErrors([]),
    }),
    [
      activePrompt,
      errors,
      handlePromptAnswer,
      progress,
      restoreItem,
      restoreSelected,
      restoring,
    ],
  );
};
