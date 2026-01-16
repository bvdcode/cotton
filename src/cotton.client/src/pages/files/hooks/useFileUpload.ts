import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useConfirm } from "material-ui-confirm";
import { nodesApi, type NodeContentDto } from "../../../shared/api/nodesApi";
import { uploadManager } from "../../../shared/upload/UploadManager";
import { resolveUploadConflicts } from "../utils/uploadConflicts";

interface UseBreadcrumb {
  id: string;
  name: string;
}

export const useFileUpload = (
  nodeId: string | null,
  breadcrumbs: UseBreadcrumb[],
  content: NodeContentDto | undefined,
) => {
  const { t } = useTranslation(["files"]);
  const confirm = useConfirm();
  const [isDragging, setIsDragging] = useState(false);

  const handleUploadFiles = useMemo(
    () => async (files: FileList | File[]) => {
      if (!nodeId) return;

      const label = breadcrumbs
        .filter((c, idx) => idx > 0 || c.name !== "Default")
        .map((c) => c.name)
        .join(" / ")
        .trim();

      const list = Array.isArray(files) ? files : Array.from(files);
      if (list.length === 0) return;

      const contentForCheck = content ?? (await nodesApi.getChildren(nodeId));

      const confirmRename = async (
        newName: string,
      ): Promise<{ confirmed: boolean }> => {
        try {
          await confirm({
            title: t("conflicts.title", { ns: "files" }),
            description: t("conflicts.description", { ns: "files", newName }),
            confirmationText: t("common:actions.confirm"),
            cancellationText: t("common:actions.cancel"),
          });
          return { confirmed: true };
        } catch {
          return { confirmed: false };
        }
      };

      const resolved = await resolveUploadConflicts(
        list,
        contentForCheck,
        confirmRename,
      );

      if (resolved.length === 0) return;

      uploadManager.enqueue(
        resolved,
        nodeId,
        label.length > 0 ? label : t("breadcrumbs.root", { ns: "files" }),
      );
    },
    [nodeId, breadcrumbs, content, confirm, t],
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

  const getAllFilesFromItems = async (
    items: DataTransferItemList,
  ): Promise<File[]> => {
    const files: File[] = [];

    const traverseEntry = async (entry: FileSystemEntry): Promise<void> => {
      if (entry.isFile) {
        const fileEntry = entry as FileSystemFileEntry;
        const file = await new Promise<File>((resolve, reject) => {
          fileEntry.file(resolve, reject);
        });
        const clonedFile = new File([file], file.name, {
          type: file.type,
          lastModified: file.lastModified,
        });
        files.push(clonedFile);
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

        const entries = await readAllEntries();
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

    return files;
  };

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    if (!isDragging) {
      setIsDragging(true);
    }
  };

  const handleDragLeave = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    if (e.currentTarget === e.target) {
      setIsDragging(false);
    }
  };

  const handleDrop = async (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);

    if (!nodeId) return;

    if (e.dataTransfer.items && e.dataTransfer.items.length > 0) {
      const files = await getAllFilesFromItems(e.dataTransfer.items);
      if (files.length > 0) {
        void handleUploadFiles(files);
      }
    } else if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      void handleUploadFiles(Array.from(e.dataTransfer.files));
    }
  };

  return {
    isDragging,
    handleUploadClick,
    handleUploadFiles,
    handleDragOver,
    handleDragLeave,
    handleDrop,
  };
};
