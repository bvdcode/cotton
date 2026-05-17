import * as React from "react";
import { toast } from "react-toastify";
import { useTranslation } from "react-i18next";
import { downloadFile } from "../utils/fileHandlers";
import { shareFile } from "../utils/shareFile";
import { getFileTypeInfo } from "@shared/utils/fileTypes";
import { buildAudioPlaylistFromFiles } from "../utils/audioPlaylistBuilder";
import { useAudioPlayerStore } from "../store/audioPlayerStore";
import { useMediaLightbox } from "./useMediaLightbox";
import { useFilePreview } from "./useFilePreview";
import type { NodeFileManifestDto } from "../api/nodesApi";
import {
  ClientEncryptionSizeLimitError,
  downloadReadableFile,
  isFileEncrypted,
  NoKeyError,
} from "../crypto";
import { taskManager, type AppTaskHandle } from "../tasks";
import { formatBytes } from "../utils/formatBytes";

interface UseFileInteractionHandlersArgs {
  sortedFiles: NodeFileManifestDto[];
}

interface UseFileInteractionHandlersResult {
  previewState: ReturnType<typeof useFilePreview>["previewState"];
  closePreview: ReturnType<typeof useFilePreview>["closePreview"];
  handleFileClick: (fileId: string, fileName: string, fileSizeBytes?: number) => void;
  handleDownloadFile: (fileId: string, fileName: string) => Promise<void>;
  handleShareFile: (fileId: string, fileName: string) => Promise<void>;
  lightboxOpen: boolean;
  lightboxIndex: number;
  mediaItems: ReturnType<typeof useMediaLightbox>["mediaItems"];
  getSignedMediaUrl: ReturnType<typeof useMediaLightbox>["getSignedMediaUrl"];
  getDownloadUrl: ReturnType<typeof useMediaLightbox>["getDownloadUrl"];
  handleMediaClick: ReturnType<typeof useMediaLightbox>["handleMediaClick"];
  setLightboxOpen: ReturnType<typeof useMediaLightbox>["setLightboxOpen"];
}

export const useFileInteractionHandlers = ({
  sortedFiles,
}: UseFileInteractionHandlersArgs): UseFileInteractionHandlersResult => {
  const { t } = useTranslation(["files", "search", "common"]);

  const { previewState, openPreview, closePreview } = useFilePreview();
  const openAudio = useAudioPlayerStore((s) => s.openFromSelection);

  const filesById = React.useMemo(
    () => new Map(sortedFiles.map((file) => [file.id, file])),
    [sortedFiles],
  );

  const inlineReadableFiles = React.useMemo(
    () => sortedFiles.filter((file) => !isFileEncrypted(file.metadata)),
    [sortedFiles],
  );

  const audioPlaylist = React.useMemo(
    () => buildAudioPlaylistFromFiles(inlineReadableFiles),
    [inlineReadableFiles],
  );

  const handleDownloadFile = React.useCallback(
    async (fileId: string, fileName: string) => {
      const file = filesById.get(fileId);

      if (!file || !isFileEncrypted(file.metadata)) {
        await downloadFile(fileId, fileName);
        return;
      }

      const decryptTaskRef: { current: AppTaskHandle | null } = {
        current: null,
      };
      let decryptTaskFinished = false;

      try {
        await downloadReadableFile(file, undefined, {
          onDecryptProgress: (bytesDecrypted, bytesTotal) => {
            decryptTaskRef.current ??= taskManager.createTask({
              kind: "decrypt",
              label: file.name,
              scopeLabel: "",
              bytesTotal,
            });
            decryptTaskRef.current.update({
              status: "running",
              bytesTotal,
              bytesCompleted: bytesDecrypted,
            });
          },
          onDecryptComplete: () => {
            decryptTaskFinished = true;
            decryptTaskRef.current?.complete();
          },
        });
      } catch (error) {
        if (decryptTaskRef.current && !decryptTaskFinished) {
          decryptTaskRef.current.fail({
            message: error instanceof Error ? error.message : undefined,
            key: error instanceof NoKeyError
              ? "encryptionVaultLocked"
              : error instanceof ClientEncryptionSizeLimitError
                ? "clientEncryptionFileTooLarge"
                : "decryptionFailed",
            params: error instanceof ClientEncryptionSizeLimitError
              ? { maxSize: formatBytes(error.maxBytes) }
              : undefined,
          });
        }

        if (error instanceof NoKeyError) {
          toast.error(t("common:clientEncryption.vaultLockedForDownload"));
          return;
        }

        if (error instanceof ClientEncryptionSizeLimitError) {
          toast.error(
            t("common:clientEncryption.fileTooLargeForDownload", {
              maxSize: formatBytes(error.maxBytes),
            }),
          );
          return;
        }

        throw error;
      }
    },
    [filesById, t],
  );

  const handleShareFile = React.useCallback(
    async (fileId: string, fileName: string) => {
      await shareFile(fileId, fileName, t);
    },
    [t],
  );

  const handleFileClick = React.useCallback(
    (fileId: string, fileName: string, fileSizeBytes?: number) => {
      const file = filesById.get(fileId);

      if (file && isFileEncrypted(file.metadata)) {
        void handleDownloadFile(fileId, fileName);
        return;
      }

      const typeInfo = getFileTypeInfo(fileName, file?.contentType ?? null, {
        requiresVideoTranscoding: file?.requiresVideoTranscoding ?? false,
      });

      if (typeInfo.type === "audio") {
        openAudio({ fileId, fileName, playlist: audioPlaylist });
        return;
      }

      const opened = openPreview(fileId, fileName, fileSizeBytes);
      if (!opened) {
        void handleDownloadFile(fileId, fileName);
      }
    },
    [audioPlaylist, filesById, handleDownloadFile, openAudio, openPreview],
  );

  const {
    lightboxOpen,
    lightboxIndex,
    mediaItems,
    getSignedMediaUrl,
    getDownloadUrl,
    handleMediaClick: openMediaLightbox,
    setLightboxOpen,
  } = useMediaLightbox(inlineReadableFiles);

  const handleMediaClick = React.useCallback(
    (fileId: string) => {
      const file = filesById.get(fileId);

      if (file && isFileEncrypted(file.metadata)) {
        void handleDownloadFile(file.id, file.name);
        return;
      }

      openMediaLightbox(fileId);
    },
    [filesById, handleDownloadFile, openMediaLightbox],
  );

  return {
    previewState,
    closePreview,
    handleFileClick,
    handleDownloadFile,
    handleShareFile,
    lightboxOpen,
    lightboxIndex,
    mediaItems,
    getSignedMediaUrl,
    getDownloadUrl,
    handleMediaClick,
    setLightboxOpen,
  };
};
