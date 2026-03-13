import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { filesApi } from "../../../shared/api/filesApi";
import { getFileTypeInfo } from "../utils/fileTypes";
import { getFileIcon } from "../utils/icons";
import type { MediaItem } from "../components";

export interface MediaHandlers {
  lightboxOpen: boolean;
  lightboxIndex: number;
  mediaItems: MediaItem[];
  getSignedMediaUrl: (fileId: string) => Promise<string>;
  getDownloadUrl: (fileId: string) => Promise<string>;
  handleMediaClick: (fileId: string) => void;
  setLightboxOpen: (open: boolean) => void;
  setLightboxIndex: (index: number) => void;
}

const LIGHTBOX_HISTORY_STATE = "lightbox";

/**
 * Hook for managing media lightbox state and handlers.
 * Pushes a history entry when opening so that mobile swipe-back
 * closes the viewer instead of navigating away.
 */
export const useMediaLightbox = (
  sortedFiles: Array<{
    id: string;
    name: string;
    sizeBytes?: number;
    previewHashEncryptedHex?: string | null;
    largeFilePreviewPresignedToken?: string | null;
    contentType?: string | null;
  }>,
): MediaHandlers => {
  const [lightboxOpen, setLightboxOpenRaw] = useState(false);
  const [lightboxIndex, setLightboxIndex] = useState(0);
  const historyPushedRef = useRef(false);

  const closeLightbox = useCallback(() => {
    setLightboxOpenRaw(false);
    if (historyPushedRef.current) {
      historyPushedRef.current = false;
      window.history.back();
    }
  }, []);

  const openLightbox = useCallback(() => {
    setLightboxOpenRaw(true);
    window.history.pushState({ overlay: LIGHTBOX_HISTORY_STATE }, "");
    historyPushedRef.current = true;
  }, []);

  useEffect(() => {
    const handlePopState = (e: PopStateEvent) => {
      if (
        historyPushedRef.current &&
        !(e.state && (e.state as { overlay?: string }).overlay === LIGHTBOX_HISTORY_STATE)
      ) {
        historyPushedRef.current = false;
        setLightboxOpenRaw(false);
      }
    };

    window.addEventListener("popstate", handlePopState);
    return () => window.removeEventListener("popstate", handlePopState);
  }, []);

  // Build media items for lightbox (images and videos only)
  const mediaItems = useMemo<MediaItem[]>(() => {
    return sortedFiles
      .map((file) => ({
        file,
        typeInfo: getFileTypeInfo(file.name, file.contentType ?? null),
      }))
      .filter(({ typeInfo }) => typeInfo.type === "image" || typeInfo.type === "video")
      .map(({ file, typeInfo }) => {
        const preview = getFileIcon(
          file.largeFilePreviewPresignedToken ?? file.previewHashEncryptedHex ?? null,
          file.name,
          file.contentType,
        );
        const previewUrl = typeof preview === "string" ? preview : "";

        return {
          id: file.id,
          kind: typeInfo.type === "image" ? "image" : "video",
          name: file.name,
          previewUrl,
          mimeType: file.contentType ?? "application/octet-stream",
          sizeBytes: file.sizeBytes,
        };
      });
  }, [sortedFiles]);

  // Get signed media URL for gallery viewing - uses lightweight server-side preview
  const getSignedMediaUrl = useCallback(async (fileId: string): Promise<string> => {
    const url = await filesApi.getDownloadLink(fileId, 60 * 24);
    return `${url}&preview=true`;
  }, []);

  // Get download URL for the actual download button - full original file
  const getDownloadUrl = useCallback(async (fileId: string): Promise<string> => {
    return await filesApi.getDownloadLink(fileId, 60 * 24);
  }, []);

  // Handler to open media lightbox
  const handleMediaClick = useCallback(
    (fileId: string) => {
      const mediaIndex = mediaItems.findIndex((item) => item.id === fileId);
      if (mediaIndex !== -1) {
        setLightboxIndex(mediaIndex);
        openLightbox();
      }
    },
    [mediaItems, openLightbox],
  );

  const setLightboxOpen = useCallback(
    (open: boolean) => {
      if (open) {
        openLightbox();
      } else {
        closeLightbox();
      }
    },
    [openLightbox, closeLightbox],
  );

  return {
    lightboxOpen,
    lightboxIndex,
    mediaItems,
    getSignedMediaUrl,
    getDownloadUrl,
    handleMediaClick,
    setLightboxOpen,
    setLightboxIndex,
  };
};
