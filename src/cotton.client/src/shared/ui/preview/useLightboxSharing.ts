import React from "react";
import type { Slide } from "yet-another-react-lightbox";
import {
  isSlideWithTitle,
  type MediaLightboxProps,
} from "@shared/types/mediaLightbox";

export const useLightboxSharing = (
  resolveSlideDownloadUrl: (slide: Slide) => Promise<string | null>,
  onShare: MediaLightboxProps["onShare"],
) => {
  const handleCustomDownload = React.useCallback(
    async ({
      slide,
      saveAs,
    }: {
      slide: Slide;
      saveAs: (source: string | Blob, name?: string) => void;
    }) => {
      if (!isSlideWithTitle(slide)) {
        return;
      }
      const downloadUrl = await resolveSlideDownloadUrl(slide);
      if (!downloadUrl) {
        return;
      }
      saveAs(downloadUrl, slide.fileName);
    },
    [resolveSlideDownloadUrl],
  );

  const handleCustomShare = React.useCallback(
    async ({ slide }: { slide: Slide }) => {
      if (!isSlideWithTitle(slide)) {
        return;
      }
      await onShare(slide.fileId, slide.fileName);
    },
    [onShare],
  );

  return { handleCustomDownload, handleCustomShare };
};
