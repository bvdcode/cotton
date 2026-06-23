import {
  useCallback,
  useMemo,
  useRef,
  useState,
  type MutableRefObject,
} from "react";
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

const getOriginalParentPath = (tile: FileSystemTile): string => {
  if (tile.kind === "folder") {
    return tile.node.metadata?.[originalParentPathMetadataKey] ?? "";
  }

  return "metadata" in tile.file
    ? (tile.file.metadata?.[originalParentPathMetadataKey] ?? "")
    : "";
};

export type RestorableItem = {
  id: string;
  kind: "folder" | "file";
  name: string;
  restorePath?: string | null;
};

type PromptKind =
  | { kind: "confirm"; restorePath: string }
  | { kind: "parentMissing"; missingPath: string }
  | {
      kind: "conflict";
      conflictKind: RestoreConflictKind;
      conflictName: string;
    };

type PromptState = {
  item: RestorableItem;
  prompt: PromptKind;
};

type RestoreSingleResult = "restored" | "skipped" | "failed";

type RestoreOptions = {
  createMissingParents: boolean;
  overwrite: boolean;
};

type RestoreOutcomeDecision =
  | { done: true; result: RestoreSingleResult }
  | { done: false; options: RestoreOptions };

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
  const resolvePromptRef = useRef<((decision: PromptDecision) => void) | null>(
    null,
  );

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

      return getOriginalParentPath(tile);
    },
    [tiles],
  );

  const callRestore = useCallback(
    (
      item: RestorableItem,
      options: { createMissingParents: boolean; overwrite: boolean },
    ) =>
      item.kind === "folder"
        ? nodesApi.restoreNode(item.id, options)
        : filesApi.restoreFile(item.id, options),
    [],
  );

  const requestStickyDecision = useCallback(
    async (
      sticky: MutableRefObject<PromptDecision["action"] | null>,
      item: RestorableItem,
      prompt: PromptKind,
    ): Promise<PromptDecision["action"]> => {
      if (sticky.current === "skip" || sticky.current === "apply") {
        return sticky.current;
      }

      const decision = await requestDecision(item, prompt);
      if (decision.applyToAll) {
        sticky.current = decision.action;
      }
      return decision.action;
    },
    [requestDecision],
  );

  const reportRestoreFailure = useCallback(
    (item: RestorableItem) => {
      setErrors((prev) => [...prev, t("restore.failed", { name: item.name })]);
    },
    [t],
  );

  const confirmRestore = useCallback(
    async (
      item: RestorableItem,
      restorePath: string,
    ): Promise<RestoreSingleResult | null> => {
      const action = await requestStickyDecision(stickyConfirm, item, {
        kind: "confirm",
        restorePath,
      });
      return action === "skip" ? "skipped" : null;
    },
    [requestStickyDecision],
  );

  const handleParentMissingOutcome = useCallback(
    async (
      item: RestorableItem,
      outcome: RestoreOutcomeDto,
      options: RestoreOptions,
    ): Promise<RestoreOutcomeDecision> => {
      const action = await requestStickyDecision(stickyParentMissing, item, {
        kind: "parentMissing",
        missingPath: outcome.missingPath ?? "",
      });

      return action === "skip"
        ? { done: true, result: "skipped" }
        : { done: false, options: { ...options, createMissingParents: true } };
    },
    [requestStickyDecision],
  );

  const handleConflictOutcome = useCallback(
    async (
      item: RestorableItem,
      outcome: RestoreOutcomeDto,
      options: RestoreOptions,
    ): Promise<RestoreOutcomeDecision> => {
      const action = await requestStickyDecision(stickyConflict, item, {
        kind: "conflict",
        conflictKind: outcome.conflictKind ?? "File",
        conflictName: outcome.conflictName ?? item.name,
      });

      return action === "skip"
        ? { done: true, result: "skipped" }
        : { done: false, options: { ...options, overwrite: true } };
    },
    [requestStickyDecision],
  );

  const handleRestoreOutcome = useCallback(
    async (
      item: RestorableItem,
      outcome: RestoreOutcomeDto,
      options: RestoreOptions,
    ): Promise<RestoreOutcomeDecision> => {
      switch (outcome.status) {
        case "Restored":
          return { done: true, result: "restored" };
        case "NotRestorable":
          setErrors((prev) => [
            ...prev,
            item.name + ": " + (outcome.reason ?? t("restore.notRestorable")),
          ]);
          return { done: true, result: "failed" };
        case "ParentMissing":
          return handleParentMissingOutcome(item, outcome, options);
        case "Conflict":
          return handleConflictOutcome(item, outcome, options);
        default:
          return { done: true, result: "failed" };
      }
    },
    [handleConflictOutcome, handleParentMissingOutcome, t],
  );

  const restoreSingle = useCallback(
    async (item: RestorableItem): Promise<RestoreSingleResult> => {
      const confirmed = await confirmRestore(item, getRestorePath(item));
      if (confirmed) {
        return confirmed;
      }

      let options: RestoreOptions = {
        createMissingParents: false,
        overwrite: false,
      };
      for (let attempt = 0; attempt < maxPromptHops; attempt += 1) {
        try {
          const outcome = await callRestore(item, options);
          const decision = await handleRestoreOutcome(item, outcome, options);
          if (decision.done) {
            return decision.result;
          }
          options = decision.options;
        } catch (error) {
          console.error("Restore call failed", error);
          reportRestoreFailure(item);
          return "failed";
        }
      }

      reportRestoreFailure(item);
      return "failed";
    },
    [
      callRestore,
      confirmRestore,
      getRestorePath,
      handleRestoreOutcome,
      reportRestoreFailure,
    ],
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
          setProgress({
            current: i + 1,
            total: items.length,
            itemName: item.name,
          });
          await restoreSingle(item);
        }
      } finally {
        restoreInFlight.current = false;
        setRestoring(false);
        setProgress({
          current: items.length,
          total: items.length,
          itemName: "",
        });
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
      .map(
        (tile): RestorableItem =>
          tile.kind === "folder"
            ? { id: tile.node.id, kind: "folder", name: tile.node.name }
            : { id: tile.file.id, kind: "file", name: tile.file.name },
      );

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
