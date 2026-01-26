import { useMemo, useState } from "react";
import { filesApi } from "../../../shared/api/filesApi";
import { isImageFile, isVideoFile } from "../utils/fileTypes";
import { getFileIcon } from "../utils/icons";
import type { MediaItem } from "../components";

export interface MediaHandlers {
  lightboxOpen: boolean;
  lightboxIndex: number;
  mediaItems: MediaItem[];
  getSignedMediaUrl: (fileId: string) => Promise<string>;
  handleMediaClick: (fileId: string) => void;
  setLightboxOpen: (open: boolean) => void;
  setLightboxIndex: (index: number) => void;
}

/**
 * Hook for managing media lightbox state and handlers
 */
export const useMediaLightbox = (
  sortedFiles: Array<{
    id: string;
    name: string;
    sizeBytes?: number;
    encryptedFilePreviewHashHex?: string | null;
    contentType?: string | null;
  }>,
): MediaHandlers => {
  const [lightboxOpen, setLightboxOpen] = useState(false);
  const [lightboxIndex, setLightboxIndex] = useState(0);

  // Build media items for lightbox (images and videos only)
  const mediaItems = useMemo<MediaItem[]>(() => {
    return sortedFiles
      .filter((file) => isImageFile(file.name) || isVideoFile(file.name))
      .map((file) => {
        const preview = getFileIcon(
          file.encryptedFilePreviewHashHex ?? null,
          file.name,
          file.contentType,
        );
        const previewUrl = typeof preview === "string" ? preview : "";

        return {
          id: file.id,
          kind: isImageFile(file.name) ? "image" : "video",
          name: file.name,
          previewUrl,
          mimeType: file.name.toLowerCase().endsWith(".mp4")
            ? "video/mp4"
            : undefined,
          sizeBytes: file.sizeBytes,
        } as MediaItem;
      });
  }, [sortedFiles]);

  // Get signed media URL for original file
  const getSignedMediaUrl = async (fileId: string): Promise<string> => {
    return await filesApi.getDownloadLink(fileId, 60 * 24);
  };

  // Handler to open media lightbox
  const handleMediaClick = (fileId: string) => {
    const mediaIndex = mediaItems.findIndex((item) => item.id === fileId);
    if (mediaIndex !== -1) {
      setLightboxIndex(mediaIndex);
      setLightboxOpen(true);
    }
  };

  return {
    lightboxOpen,
    lightboxIndex,
    mediaItems,
    getSignedMediaUrl,
    handleMediaClick,
    setLightboxOpen,
    setLightboxIndex,
  };
};
