import { useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  nodesApi,
  type NodeContentDto,
  type NodeFileManifestDto,
} from "../../../shared/api/nodesApi";
import { uploadManager } from "../../../shared/upload/UploadManager";
import { ConflictAction } from "../../../shared/types/nameConflict";
import { resolveUploadConflicts } from "../utils/uploadConflicts";
import { showActionToast } from "../../../shared/ui/ActionToast";
import { toast } from "../../../shared/ui/notifications";
import { useFileConflictDialog } from "./useFileConflictDialog";
import { useUploadEncryptionPolicy } from "./useUploadEncryptionPolicy";
import { hasFileDragPayload } from "./hasFileDragPayload";
import type {
  UseBreadcrumb,
  DropPreparationState,
  SkippedItemsDialogState,
} from "./fileUploadTypes";
import { getAllFilesFromItems, type DroppedFile } from "./scanDroppedFiles";
import { DroppedFolderResolver } from "./DroppedFolderResolver";
import { emptySkippedItemsDialog } from "./fileUploadTypes";

type UploadToastVariant = "info" | "error";

type FileUploadOptions = {
  onToast?: (message: string, variant?: UploadToastVariant) => void;
  onFileUploaded?: (file: NodeFileManifestDto) => void;
};

export const useFileUpload = (
  nodeId: string | null,
  breadcrumbs: UseBreadcrumb[],
  content: NodeContentDto | undefined,
  options?: FileUploadOptions,
) => {
  const { t } = useTranslation(["files"]);
  const [isDragging, setIsDragging] = useState(false);
  const dragDepthRef = useRef<number>(0);
  const [dropPreparation, setDropPreparation] = useState<DropPreparationState>({
    active: false,
    phase: "idle",
    step: "idle",
    filesFound: 0,
    processed: 0,
  });
  const { dialogState, showConflictDialog, handleResolve, handleExited } =
    useFileConflictDialog();
  const [skippedItemsDialog, setSkippedItemsDialog] =
    useState<SkippedItemsDialogState>(emptySkippedItemsDialog);
  const skipAllConflictsRef = useRef<boolean>(false);
  const skippedItemsToastIdRef = useRef<string | null>(null);
  const skippedItemsToastSequenceRef = useRef(0);
  const onToast = options?.onToast;
  const onFileUploaded = options?.onFileUploaded;

  const baseLabel = useMemo(() => {
    const label = breadcrumbs
      .filter((c, idx) => idx > 0 || c.name !== "Default")
      .map((c) => c.name)
      .join(" / ")
      .trim();
    return label.length > 0 ? label : t("breadcrumbs.root", { ns: "files" });
  }, [breadcrumbs, t]);

  const { isPolicyEnabledForNode, decideEncrypt } = useUploadEncryptionPolicy();

  const handleUploadFiles = useMemo(
    () => async (files: FileList | File[]) => {
      if (!nodeId) return;

      const list = Array.isArray(files) ? files : Array.from(files);
      if (list.length === 0) return;

      const decision = decideEncrypt(nodeId);
      if (decision.vaultLocked) {
        onToast?.(t("uploadDrop.toasts.vaultLocked", { ns: "files" }), "error");
        return;
      }

      skipAllConflictsRef.current = false;

      const contentForCheck =
        content ?? (await nodesApi.getChildren(nodeId)).content;

      const confirmConflict = async (
        prompt: Parameters<typeof showConflictDialog>[0],
      ): Promise<ConflictAction> => {
        if (skipAllConflictsRef.current) {
          return ConflictAction.SkipAll;
        }

        const action = await showConflictDialog(prompt);
        if (action === ConflictAction.SkipAll) {
          skipAllConflictsRef.current = true;
        }
        return action;
      };

      const result = await resolveUploadConflicts(
        list,
        contentForCheck,
        confirmConflict,
      );

      if (result.cancelled) return;

      if (result.files.length === 0) {
        if (skipAllConflictsRef.current) {
          onToast?.(t("uploadDrop.toasts.noneCopied", { ns: "files" }));
        }
        return;
      }

      uploadManager.enqueue(result.files, nodeId, baseLabel, {
        encrypt: decision.encrypt,
        onFileUploaded,
      });
    },
    [
      nodeId,
      content,
      baseLabel,
      showConflictDialog,
      onToast,
      onFileUploaded,
      t,
      decideEncrypt,
    ],
  );

  const handleUploadDroppedFiles = useMemo(
    () => async (dropped: DroppedFile[]) => {
      if (!nodeId) return;
      if (dropped.length === 0) return;

      const rootPolicyEnabled = isPolicyEnabledForNode(nodeId);
      const rootDecision = decideEncrypt(nodeId);
      if (rootDecision.vaultLocked) {
        onToast?.(t("uploadDrop.toasts.vaultLocked", { ns: "files" }), "error");
        return;
      }

      setDropPreparation((prev) => ({
        ...prev,
        active: true,
        phase: "preparing",
        step: "folders",
        filesFound: dropped.length,
        processed: 0,
      }));

      skipAllConflictsRef.current = false;

      let totalEnqueued = 0;

      const confirmConflict = async (
        prompt: Parameters<typeof showConflictDialog>[0],
      ): Promise<ConflictAction> => {
        if (skipAllConflictsRef.current) {
          return ConflictAction.SkipAll;
        }

        const action = await showConflictDialog(prompt);
        if (action === ConflictAction.SkipAll) {
          skipAllConflictsRef.current = true;
        }
        return action;
      };

      const folderResolver = new DroppedFolderResolver(
        nodeId,
        baseLabel,
        rootPolicyEnabled,
        isPolicyEnabledForNode,
      );

      let lastProgressTime = 0;
      const updateProgress = (processed: number) => {
        const now = Date.now();
        if (now - lastProgressTime < 120 && processed < dropped.length) return;
        lastProgressTime = now;
        setDropPreparation((prev) => ({
          ...prev,
          active: true,
          phase: "preparing",
          step: "folders",
          processed,
          filesFound: dropped.length,
        }));
      };

      const filesByTarget = await folderResolver.groupFiles(
        dropped,
        updateProgress,
      );

      setDropPreparation((prev) => ({
        ...prev,
        active: true,
        phase: "preparing",
        step: "conflicts",
        filesFound: dropped.length,
        processed: dropped.length,
      }));

      for (const [targetNodeId, bucket] of filesByTarget) {
        const contentForCheck = (await nodesApi.getChildren(targetNodeId))
          .content;
        const result = await resolveUploadConflicts(
          bucket.files,
          contentForCheck,
          confirmConflict,
        );
        if (result.cancelled) return;
        if (result.files.length === 0) continue;

        setDropPreparation((prev) => ({
          ...prev,
          active: true,
          phase: "preparing",
          step: "enqueue",
          filesFound: dropped.length,
          processed: dropped.length,
        }));
        const decision = folderResolver.decideEncryption(targetNodeId);
        if (decision.vaultLocked) {
          onToast?.(
            t("uploadDrop.toasts.vaultLocked", { ns: "files" }),
            "error",
          );
          continue;
        }

        uploadManager.enqueue(result.files, targetNodeId, bucket.label, {
          encrypt: decision.encrypt,
          onFileUploaded,
        });
        totalEnqueued += result.files.length;
      }

      if (totalEnqueued === 0 && skipAllConflictsRef.current) {
        onToast?.(t("uploadDrop.toasts.noneCopied", { ns: "files" }));
      }
    },
    [
      nodeId,
      baseLabel,
      showConflictDialog,
      onToast,
      onFileUploaded,
      t,
      decideEncrypt,
      isPolicyEnabledForNode,
    ],
  );

  const handleUploadClick = () => {
    if (!nodeId) return;
    const input = document.createElement("input");
    input.type = "file";
    input.multiple = true;
    input.onchange = (e) => {
      if (!(e.target instanceof HTMLInputElement)) return;
      const files = e.target.files;
      if (files && files.length > 0) {
        void handleUploadFiles(Array.from(files));
      }
    };
    input.click();
  };

  const handleDragEnter = (e: React.DragEvent) => {
    if (!hasFileDragPayload(e.dataTransfer)) {
      return;
    }

    e.preventDefault();
    e.stopPropagation();
    dragDepthRef.current += 1;
    if (!isDragging) {
      setIsDragging(true);
    }
  };

  const handleDragOver = (e: React.DragEvent) => {
    if (!hasFileDragPayload(e.dataTransfer)) {
      return;
    }

    e.preventDefault();
    e.stopPropagation();
    e.dataTransfer.dropEffect = "copy";
    if (!isDragging) setIsDragging(true);
  };

  const handleDragLeave = (e: React.DragEvent) => {
    if (!isDragging) {
      return;
    }

    e.preventDefault();
    e.stopPropagation();

    dragDepthRef.current = Math.max(0, dragDepthRef.current - 1);
    if (dragDepthRef.current === 0) {
      setIsDragging(false);
    }
  };

  const handleDrop = async (e: React.DragEvent) => {
    if (!hasFileDragPayload(e.dataTransfer)) {
      return;
    }

    e.preventDefault();
    e.stopPropagation();
    dragDepthRef.current = 0;
    setIsDragging(false);

    if (!nodeId) return;

    const hasItems = e.dataTransfer.items && e.dataTransfer.items.length > 0;
    const hasFiles = e.dataTransfer.files && e.dataTransfer.files.length > 0;

    if (hasItems) {
      setDropPreparation({
        active: true,
        phase: "scanning",
        step: "scanning",
        filesFound: 0,
        processed: 0,
      });

      try {
        const scan = await getAllFilesFromItems(
          e.dataTransfer.items,
          (filesFound) =>
            setDropPreparation((prev) => ({
              ...prev,
              active: true,
              phase: "scanning",
              step: "scanning",
              filesFound,
              processed: 0,
            })),
        );

        if (scan.skippedNotFound > 0) {
          const details = {
            open: false,
            total: scan.skippedNotFound,
            items: scan.skippedItems,
            truncated: scan.skippedItems.length < scan.skippedNotFound,
          };
          setSkippedItemsDialog(details);
          if (skippedItemsToastIdRef.current) {
            toast.dismiss(skippedItemsToastIdRef.current);
          }
          skippedItemsToastSequenceRef.current += 1;
          const toastId = `files-upload-skipped-${skippedItemsToastSequenceRef.current}`;
          skippedItemsToastIdRef.current = toastId;

          showActionToast({
            toastId,
            message: t("uploadDrop.toasts.someItemsSkipped", {
              ns: "files",
              count: scan.skippedNotFound,
            }),
            action: t("uploadDrop.toasts.details", { ns: "files" }),
            onAction: () => setSkippedItemsDialog({ ...details, open: true }),
          });
        }

        if (scan.files.length > 0) {
          setDropPreparation((prev) => ({
            ...prev,
            active: true,
            phase: "preparing",
            step: "mapping",
            filesFound: scan.files.length,
            processed: 0,
          }));
          await handleUploadDroppedFiles(scan.files);
        }
      } catch {
        onToast?.(t("uploadDrop.errors.dropFailed", { ns: "files" }), "error");
      } finally {
        setDropPreparation({
          active: false,
          phase: "idle",
          step: "idle",
          filesFound: 0,
          processed: 0,
        });
      }

      return;
    }

    if (hasFiles) {
      setDropPreparation({
        active: true,
        phase: "preparing",
        step: "conflicts",
        filesFound: e.dataTransfer.files.length,
        processed: e.dataTransfer.files.length,
      });

      try {
        await handleUploadFiles(Array.from(e.dataTransfer.files));
      } catch {
        onToast?.(t("uploadDrop.errors.dropFailed", { ns: "files" }), "error");
      } finally {
        setDropPreparation({
          active: false,
          phase: "idle",
          step: "idle",
          filesFound: 0,
          processed: 0,
        });
      }
    }
  };

  const closeSkippedItemsDialog = () => {
    setSkippedItemsDialog((current) => ({ ...current, open: false }));
  };

  return {
    isDragging,
    dropPreparation,
    skippedItemsDialog: {
      state: skippedItemsDialog,
      onClose: closeSkippedItemsDialog,
    },
    handleUploadClick,
    handleUploadFiles,
    handleDragEnter,
    handleDragOver,
    handleDragLeave,
    handleDrop,
    conflictDialog: {
      state: dialogState,
      onResolve: handleResolve,
      onExited: handleExited,
    },
  };
};
