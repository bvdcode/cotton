import { useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { nodesApi, type NodeContentDto } from "../../../shared/api/nodesApi";
import { useNodesStore } from "../../../shared/store/nodesStore";
import { uploadManager } from "../../../shared/upload/UploadManager";
import {
  resolveUploadConflicts,
  ConflictAction,
} from "../utils/uploadConflicts";
import { useFileConflictDialog } from "./useFileConflictDialog";

interface UseBreadcrumb {
  id: string;
  name: string;
}

type DroppedFile = {
  file: File;
  relativePath: string;
};

type DropPreparationPhase = "idle" | "scanning" | "preparing";

type DropPreparationStep =
  | "idle"
  | "scanning"
  | "mapping"
  | "folders"
  | "conflicts"
  | "enqueue";

type DropPreparationState = {
  active: boolean;
  phase: DropPreparationPhase;
  step: DropPreparationStep;
  filesFound: number;
  processed: number;
};

type FileUploadOptions = {
  onToast?: (message: string) => void;
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
  const skipAllConflictsRef = useRef<boolean>(false);
  const onToast = options?.onToast;

  const baseLabel = useMemo(() => {
    const label = breadcrumbs
      .filter((c, idx) => idx > 0 || c.name !== "Default")
      .map((c) => c.name)
      .join(" / ")
      .trim();
    return label.length > 0 ? label : t("breadcrumbs.root", { ns: "files" });
  }, [breadcrumbs, t]);

  const handleUploadFiles = useMemo(
    () => async (files: FileList | File[]) => {
      if (!nodeId) return;

      const list = Array.isArray(files) ? files : Array.from(files);
      if (list.length === 0) return;

      skipAllConflictsRef.current = false;

      const contentForCheck =
        content ?? (await nodesApi.getChildren(nodeId)).content;

      const confirmRename = async (
        newName: string,
      ): Promise<ConflictAction> => {
        if (skipAllConflictsRef.current) {
          return ConflictAction.SkipAll;
        }

        const action = await showConflictDialog(newName);
        if (action === ConflictAction.SkipAll) {
          skipAllConflictsRef.current = true;
        }
        return action;
      };

      const result = await resolveUploadConflicts(
        list,
        contentForCheck,
        confirmRename,
      );

      if (result.cancelled) return;

      if (result.files.length === 0) {
        if (skipAllConflictsRef.current) {
          onToast?.(t("uploadDrop.toasts.noneCopied", { ns: "files" }));
        }
        return;
      }

      uploadManager.enqueue(result.files, nodeId, baseLabel);
    },
    [nodeId, content, baseLabel, showConflictDialog, onToast, t],
  );

  const handleUploadDroppedFiles = useMemo(
    () => async (dropped: DroppedFile[]) => {
      if (!nodeId) return;
      if (dropped.length === 0) return;

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

      const confirmRename = async (
        newName: string,
      ): Promise<ConflictAction> => {
        if (skipAllConflictsRef.current) {
          return ConflictAction.SkipAll;
        }

        const action = await showConflictDialog(newName);
        if (action === ConflictAction.SkipAll) {
          skipAllConflictsRef.current = true;
        }
        return action;
      };

      const folderIdByKey = new Map<string, string>();
      const childrenByNodeId = new Map<string, NodeContentDto>();

      const getChildrenCached = async (id: string): Promise<NodeContentDto> => {
        const cached = childrenByNodeId.get(id);
        if (cached) return cached;
        const loaded = await nodesApi.getChildren(id);
        childrenByNodeId.set(id, loaded.content);
        return loaded.content;
      };

      const findAvailableFolderName = async (
        parentId: string,
        baseName: string,
      ): Promise<string> => {
        const content = await getChildrenCached(parentId);
        const takenLower = new Set<string>([
          ...content.nodes.map((n) => n.name.toLowerCase()),
          ...content.files.map((f) => f.name.toLowerCase()),
        ]);

        const preferred = `${baseName} (folder)`;
        if (!takenLower.has(preferred.toLowerCase())) return preferred;

        for (let i = 2; i < 10_000; i += 1) {
          const candidate = `${baseName} (folder ${i})`;
          if (!takenLower.has(candidate.toLowerCase())) return candidate;
        }
        return `${baseName}-${Date.now()}`;
      };

      const ensureFolder = async (
        parentId: string,
        desiredName: string,
      ): Promise<{ id: string; name: string }> => {
        const key = `${parentId}::${desiredName}`;
        const cachedId = folderIdByKey.get(key);
        if (cachedId) return { id: cachedId, name: desiredName };

        const content = await getChildrenCached(parentId);
        const existing = content.nodes.find((n) => n.name === desiredName);
        if (existing) {
          folderIdByKey.set(key, existing.id);
          return { id: existing.id, name: desiredName };
        }

        // If a file exists with the same name, we can't create a folder with that name.
        const hasFileConflict = content.files.some(
          (f) => f.name === desiredName,
        );
        const nameToCreate = hasFileConflict
          ? await findAvailableFolderName(parentId, desiredName)
          : desiredName;

        const created = await nodesApi.createNode({
          parentId,
          name: nameToCreate,
        });
        // Update caches optimistically.
        content.nodes.push(created);
        useNodesStore.getState().addFolderToCache(parentId, created);
        folderIdByKey.set(`${parentId}::${nameToCreate}`, created.id);

        return { id: created.id, name: nameToCreate };
      };

      const ensureFolderPath = async (
        rootId: string,
        segments: string[],
      ): Promise<{ nodeId: string; labelSuffix: string }> => {
        let currentId = rootId;
        const effectiveSegments: string[] = [];

        for (const raw of segments) {
          const seg = raw.trim();
          if (seg.length === 0) continue;
          const next = await ensureFolder(currentId, seg);
          currentId = next.id;
          effectiveSegments.push(next.name);
        }

        return {
          nodeId: currentId,
          labelSuffix: effectiveSegments.join(" / "),
        };
      };

      const filesByTarget = new Map<string, { label: string; files: File[] }>();

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

      updateProgress(0);

      for (let i = 0; i < dropped.length; i += 1) {
        updateProgress(i);

        const item = dropped[i];
        const normalized = item.relativePath.replace(/^\\+|^\/+/, "");
        const parts = normalized.split(/[\\/]+/).filter((p) => p.length > 0);

        // If we somehow don't get a filename, fall back to the File's name.
        if (parts.length === 0) {
          const bucket = filesByTarget.get(nodeId) ?? {
            label: baseLabel,
            files: [],
          };
          bucket.files.push(item.file);
          filesByTarget.set(nodeId, bucket);
          continue;
        }

        parts.pop();
        const { nodeId: targetNodeId, labelSuffix } = await ensureFolderPath(
          nodeId,
          parts,
        );
        const label =
          labelSuffix.length > 0 ? `${baseLabel} / ${labelSuffix}` : baseLabel;

        const bucket = filesByTarget.get(targetNodeId) ?? { label, files: [] };
        bucket.files.push(item.file);
        filesByTarget.set(targetNodeId, bucket);
      }

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
          confirmRename,
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
        uploadManager.enqueue(result.files, targetNodeId, bucket.label);
        totalEnqueued += result.files.length;
      }

      if (totalEnqueued === 0 && skipAllConflictsRef.current) {
        onToast?.(t("uploadDrop.toasts.noneCopied", { ns: "files" }));
      }
    },
    [nodeId, baseLabel, showConflictDialog, onToast, t],
  );

  const handleUploadClick = () => {
    if (!nodeId) return;
    const input = document.createElement("input");
    input.type = "file";
    input.multiple = true;
    input.onchange = (e) => {
      const files = (e.target as HTMLInputElement).files;
      if (files && files.length > 0) {
        void handleUploadFiles(Array.from(files));
      }
    };
    input.click();
  };

  type DroppedScanResult = {
    files: DroppedFile[];
    skippedNotFound: number;
  };

  type NamedErrorLike = { name?: string };

  const isNotFoundError = (
    error: DOMException | Error | NamedErrorLike | null | undefined,
  ): boolean => {
    if (error instanceof DOMException) return error.name === "NotFoundError";
    if (error instanceof Error) return error.name === "NotFoundError";
    if (typeof error === "object" && error !== null) {
      const maybe = error as NamedErrorLike;
      return maybe.name === "NotFoundError";
    }
    return false;
  };

  const getAllFilesFromItems = async (
    items: DataTransferItemList,
    onFileFound: (filesFound: number) => void,
  ): Promise<DroppedScanResult> => {
    const files: DroppedFile[] = [];
    let skippedNotFound = 0;

    let lastNotifiedCount = 0;
    let lastNotifyTime = 0;
    const notify = () => {
      const now = Date.now();
      const count = files.length;
      if (count === lastNotifiedCount) return;
      if (now - lastNotifyTime < 120 && count < 10_000) return;
      lastNotifiedCount = count;
      lastNotifyTime = now;
      onFileFound(count);
    };

    const traverseEntry = async (entry: FileSystemEntry): Promise<void> => {
      if (entry.isFile) {
        const fileEntry = entry as FileSystemFileEntry;
        let file: File;
        try {
          file = await new Promise<File>((resolve, reject) => {
            fileEntry.file(resolve, reject);
          });
        } catch (e) {
          if (isNotFoundError(e as Error)) {
            skippedNotFound += 1;
            return;
          }
          throw e;
        }
        const clonedFile = new File([file], file.name, {
          type: file.type,
          lastModified: file.lastModified,
        });

        const fullPath = (entry as { fullPath?: string }).fullPath;
        const relativePath = (fullPath ?? file.name).replace(/^\/+/, "");
        files.push({ file: clonedFile, relativePath });
        notify();
      } else if (entry.isDirectory) {
        const dirEntry = entry as FileSystemDirectoryEntry;
        const reader = dirEntry.createReader();

        const readAllEntries = async (): Promise<FileSystemEntry[]> => {
          const allEntries: FileSystemEntry[] = [];
          let batch: FileSystemEntry[] = [];

          do {
            batch = await new Promise<FileSystemEntry[]>((resolve, reject) => {
              reader.readEntries(resolve, reject);
            });
            allEntries.push(...batch);
          } while (batch.length > 0);

          return allEntries;
        };

        let entries: FileSystemEntry[];
        try {
          entries = await readAllEntries();
        } catch (e) {
          if (isNotFoundError(e as Error)) {
            skippedNotFound += 1;
            return;
          }
          throw e;
        }
        for (const childEntry of entries) {
          await traverseEntry(childEntry);
        }
      }
    };

    const promises: Promise<void>[] = [];
    for (let i = 0; i < items.length; i++) {
      const item = items[i];
      if (item.kind === "file") {
        const entry = item.webkitGetAsEntry();
        if (entry) {
          promises.push(traverseEntry(entry));
        }
      }
    }
    await Promise.all(promises);

    return { files, skippedNotFound };
  };

  const handleDragEnter = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    dragDepthRef.current += 1;
    if (!isDragging) {
      setIsDragging(true);
    }
  };

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    if (!isDragging) setIsDragging(true);
  };

  const handleDragLeave = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();

    dragDepthRef.current = Math.max(0, dragDepthRef.current - 1);
    if (dragDepthRef.current === 0) {
      setIsDragging(false);
    }
  };

  const handleDrop = async (e: React.DragEvent) => {
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
          onToast?.(
            t("uploadDrop.toasts.someItemsSkipped", {
              ns: "files",
              count: scan.skippedNotFound,
            }),
          );
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
        onToast?.(t("uploadDrop.errors.dropFailed", { ns: "files" }));
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
        onToast?.(t("uploadDrop.errors.dropFailed", { ns: "files" }));
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

  return {
    isDragging,
    dropPreparation,
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
