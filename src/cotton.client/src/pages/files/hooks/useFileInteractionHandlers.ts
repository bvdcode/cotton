import * as React from "react";
import { useTranslation } from "react-i18next";
import { downloadFile } from "../utils/fileHandlers";
import { shareFile } from "../../../shared/utils/shareFile";
import { getFileTypeInfo } from "../utils/fileTypes";
import { buildAudioPlaylistFromFiles } from "../../../shared/utils/audioPlaylistBuilder";
import { useAudioPlayerStore } from "../../../shared/store/audioPlayerStore";
import { useMediaLightbox } from "./useMediaLightbox";
import { useFilePreview } from "./useFilePreview";
import type { NodeFileManifestDto } from "../../../shared/api/nodesApi";

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

  const audioPlaylist = React.useMemo(
    () => buildAudioPlaylistFromFiles(sortedFiles),
    [sortedFiles],
  );

  const handleDownloadFile = React.useCallback(
    async (fileId: string, fileName: string) => {
      await downloadFile(fileId, fileName);
    },
    [],
  );

  const handleShareFile = React.useCallback(
    async (fileId: string, fileName: string) => {
      await shareFile(fileId, fileName, t);
    },
    [t],
  );

  const handleFileClick = React.useCallback(
    (fileId: string, fileName: string, fileSizeBytes?: number) => {
      if (getFileTypeInfo(fileName, null).type === "audio") {
        openAudio({ fileId, fileName, playlist: audioPlaylist });
        return;
      }

      const opened = openPreview(fileId, fileName, fileSizeBytes);
      if (!opened) {
        void handleDownloadFile(fileId, fileName);
      }
    },
    [audioPlaylist, handleDownloadFile, openAudio, openPreview],
  );

  const {
    lightboxOpen,
    lightboxIndex,
    mediaItems,
    getSignedMediaUrl,
    getDownloadUrl,
    handleMediaClick,
    setLightboxOpen,
  } = useMediaLightbox(sortedFiles);

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
