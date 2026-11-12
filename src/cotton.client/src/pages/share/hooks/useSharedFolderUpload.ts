import { useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import type { DragEvent } from "react";
import type { SharedNodeInfoDto } from "@shared/api/sharedFoldersApi";
import { sharedFoldersApi } from "@shared/api/sharedFoldersApi";
import { uploadManager } from "@shared/upload/UploadManager";
import { getFileNameKey } from "@shared/utils/fileNameUtils";
import { toast } from "@shared/ui/notifications";
import { hasFileDragPayload } from "../../files/hooks/hasFileDragPayload";
import { useFileConflictDialog } from "../../files/hooks/useFileConflictDialog";
import { resolveUploadConflicts } from "../../files/utils/uploadConflicts";

export const useSharedFolderUpload = (
  info: SharedNodeInfoDto,
  nodeId: string,
  label: string,
  onUploaded: () => void,
  onQueued?: (ids: string[]) => void,
) => {
  const { t } = useTranslation("share");
  const sessionId = useRef<string | null>(null);
  const [isDragging, setIsDragging] = useState(false);
  const depth = useRef(0);
  const preparing = useRef(false);
  const conflicts = useFileConflictDialog();

  const enqueue = async (files: FileList | File[]) => {
    if (!info.allowUpload || preparing.current) {
      return;
    }
    const list = Array.from(files);
    if (list.length === 0) {
      return;
    }
    preparing.current = true;
    try {
      let queuedFiles = list.map((file) => ({ file }));
      if (!info.hideFolderContent) {
        const takenNameKeys: string[] = [];
        let page = 1;
        let remaining = true;
        while (remaining) {
          const response = await sharedFoldersApi.getChildren(info.token, {
            nodeId,
            page,
            pageSize: 1000,
          });
          takenNameKeys.push(
            ...response.content.nodes.map((node) => getFileNameKey(node.name)),
            ...response.content.files.map((file) => getFileNameKey(file.name)),
          );
          remaining = page * 1000 < response.totalCount;
          page += 1;
        }
        const result = await resolveUploadConflicts(
          list,
          { nodes: [], files: [], takenNameKeys },
          conflicts.showConflictDialog,
        );
        if (result.cancelled) {
          return;
        }
        queuedFiles = result.files;
      }
      sessionId.current ??= crypto.randomUUID();
      const ids = uploadManager.enqueue(queuedFiles, nodeId, label, {
        sharedUpload: {
          token: info.token,
          sessionId: sessionId.current,
          server: info,
        },
        onFileUploaded: onUploaded,
      });
      onQueued?.(ids);
    } catch {
      toast.error(t("folder.uploadFailed"));
    } finally {
      preparing.current = false;
    }
  };

  const dragHandlers = {
    onDragEnter: (event: DragEvent) => {
      if (!info.allowUpload || !hasFileDragPayload(event.dataTransfer)) {
        return;
      }
      event.preventDefault();
      depth.current += 1;
      setIsDragging(true);
    },
    onDragLeave: (event: DragEvent) => {
      event.preventDefault();
      depth.current = Math.max(0, depth.current - 1);
      if (depth.current === 0) {
        setIsDragging(false);
      }
    },
    onDragOver: (event: DragEvent) => {
      if (info.allowUpload && hasFileDragPayload(event.dataTransfer)) {
        event.preventDefault();
        event.dataTransfer.dropEffect = "copy";
      }
    },
    onDrop: (event: DragEvent) => {
      if (!info.allowUpload || !hasFileDragPayload(event.dataTransfer)) {
        return;
      }
      event.preventDefault();
      event.stopPropagation();
      depth.current = 0;
      setIsDragging(false);
      if (
        Array.from(event.dataTransfer.items).some(
          (item) => item.webkitGetAsEntry?.()?.isDirectory,
        )
      ) {
        toast.error(t("folder.filesOnly"));
        return;
      }
      void enqueue(event.dataTransfer.files);
    },
  };

  return {
    isDragging,
    dragHandlers,
    conflicts,
    openPicker: () =>
      uploadManager.openFilePicker({
        nodeId,
        nodeLabel: label,
        onFilesSelected: (files) => void enqueue(files),
      }),
  };
};
