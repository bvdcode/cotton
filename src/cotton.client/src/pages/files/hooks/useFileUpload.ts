import { useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { type NodeFileManifestDto } from "../../../shared/api/nodesApi";
import { lookupUploadNames } from "../utils/lookupUploadNames";
import { uploadManager } from "../../../shared/upload/UploadManager";
import { createUploadConflictResolver } from "./createUploadConflictResolver";
import { resolveUploadConflicts } from "../utils/uploadConflicts";
import { showActionToast } from "../../../shared/ui/ActionToast";
import { toast } from "../../../shared/ui/notifications";
import { useFileConflictDialog } from "./useFileConflictDialog";
import { useUploadEncryptionPolicy } from "./useUploadEncryptionPolicy";
import { hasFileDragPayload } from "./hasFileDragPayload";
import type { UseBreadcrumb, SkippedItemsDialogState } from "./fileUploadTypes";
import {
  getAllFilesFromEntries,
  getDroppedEntries,
  type DroppedFile,
} from "./scanDroppedFiles";
import { prepareDroppedUpload } from "./prepareDroppedUpload";
import { useUploadPreparation } from "./useUploadPreparation";
import { emptySkippedItemsDialog } from "./fileUploadTypes";
import { reportClientError } from "@shared/utils/clientDiagnostics";

type UploadToastVariant = "info" | "error";

type FileUploadOptions = {
  onToast?: (message: string, variant?: UploadToastVariant) => void;
  onFileUploaded?: (file: NodeFileManifestDto) => void;
};

export const useFileUpload = (
  nodeId: string | null,
  breadcrumbs: UseBreadcrumb[],
  options?: FileUploadOptions,
) => {
  const { t } = useTranslation(["files"]);
  const [isDragging, setIsDragging] = useState(false);
  const dragDepthRef = useRef<number>(0);
  const { dropPreparation, setDropPreparation, run } = useUploadPreparation();
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

  const prepareFiles = useMemo(
    () => async (files: FileList | File[]) => {
      if (!nodeId) return;

      const list = Array.isArray(files) ? files : Array.from(files);
      if (list.length === 0) return;

      const decision = await decideEncrypt(nodeId);
      if (decision.vaultLocked) {
        onToast?.(t("uploadDrop.toasts.vaultLocked", { ns: "files" }), "error");
        return;
      }

      skipAllConflictsRef.current = false;

      const contentForCheck = await lookupUploadNames(
        nodeId,
        list.map((file) => file.name),
        decision.encrypt,
      );

      const confirmConflict = createUploadConflictResolver(
        skipAllConflictsRef,
        showConflictDialog,
      );

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
      baseLabel,
      showConflictDialog,
      onToast,
      onFileUploaded,
      t,
      decideEncrypt,
    ],
  );

  const handleUploadFiles = useMemo(
    () => (files: FileList | File[]) => {
      const list = Array.from(files);
      if (!nodeId || list.length === 0) {
        return Promise.resolve();
      }
      return run(async () => {
        setDropPreparation({
          active: true,
          phase: "preparing",
          step: "conflicts",
          filesFound: list.length,
          processed: 0,
        });
        await prepareFiles(list);
      });
    },
    [nodeId, prepareFiles, run, setDropPreparation],
  );

  const handleUploadDroppedFiles = async (dropped: DroppedFile[]) => {
    if (!nodeId || dropped.length === 0) {
      return;
    }
    const rootDecision = await decideEncrypt(nodeId);
    if (rootDecision.vaultLocked) {
      onToast?.(t("uploadDrop.toasts.vaultLocked"), "error");
      return;
    }
    skipAllConflictsRef.current = false;
    const count = await prepareDroppedUpload(dropped, {
      nodeId,
      baseLabel,
      rootPolicyEnabled: rootDecision.encrypt,
      isPolicyEnabledForNode,
      confirmConflict: createUploadConflictResolver(
        skipAllConflictsRef,
        showConflictDialog,
      ),
      onProgress: setDropPreparation,
      onVaultLocked: () =>
        onToast?.(t("uploadDrop.toasts.vaultLocked"), "error"),
      onFileUploaded,
    });
    if (count === 0 && skipAllConflictsRef.current) {
      onToast?.(t("uploadDrop.toasts.noneCopied"));
    }
  };

  const handleUploadClick = () => {
    if (!nodeId) return;
    const input = document.createElement("input");
    input.type = "file";
    input.multiple = true;
    input.onchange = (e) => {
      if (!(e.target instanceof HTMLInputElement)) return;
      const files = e.target.files;
      if (files && files.length > 0) {
        void handleUploadFiles(Array.from(files)).catch((error) => {
          reportClientError("Failed to prepare file upload", error);
          onToast?.(t("uploadDrop.errors.dropFailed"), "error");
        });
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

    const entries =
      e.dataTransfer.items.length > 0
        ? getDroppedEntries(e.dataTransfer.items)
        : null;
    const files = Array.from(e.dataTransfer.files);
    await run(async () => {
      if (entries !== null) {
        setDropPreparation({
          active: true,
          phase: "scanning",
          step: "scanning",
          filesFound: 0,
          processed: 0,
        });

        try {
          const scan = await getAllFilesFromEntries(entries, (filesFound) =>
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
          onToast?.(
            t("uploadDrop.errors.dropFailed", { ns: "files" }),
            "error",
          );
        }

        return;
      }

      if (files.length > 0) {
        setDropPreparation({
          active: true,
          phase: "preparing",
          step: "conflicts",
          filesFound: files.length,
          processed: files.length,
        });

        try {
          await prepareFiles(files);
        } catch {
          onToast?.(
            t("uploadDrop.errors.dropFailed", { ns: "files" }),
            "error",
          );
        }
      }
    });
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
