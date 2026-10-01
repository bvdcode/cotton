import React from "react";
import type { Slide } from "yet-another-react-lightbox";
import { isSlideWithTitle } from "@shared/types/mediaLightbox";
import { shareLinks } from "../../utils/shareLinks";

export const useLightboxSharing = (
  resolveSlideDownloadUrl: (slide: Slide) => Promise<string | null>,
) => {
  const handleCustomDownload = React.useCallback(
    async ({
      slide,
      saveAs,
    }: {
      slide: Slide;
      saveAs: (source: string | Blob, name?: string) => void;
    }) => {
      if (!isSlideWithTitle(slide)) return;
      const downloadUrl = await resolveSlideDownloadUrl(slide);
      if (!downloadUrl) return;
      saveAs(downloadUrl, slide.fileName);
    },
    [resolveSlideDownloadUrl],
  );

  const handleCustomShare = React.useCallback(
    async ({ slide }: { slide: Slide }) => {
      if (!isSlideWithTitle(slide)) return;
      if (!navigator.canShare) return;

      const downloadUrl = await resolveSlideDownloadUrl(slide);
      if (!downloadUrl) return;

      const token = shareLinks.tryExtractTokenFromDownloadUrl(downloadUrl);
      const shareUrl = token ? shareLinks.buildShareUrl(token) : downloadUrl;
      const sharePayload = { title: slide.fileName, url: shareUrl };

      if (!navigator.canShare(sharePayload)) return;

      navigator.share(sharePayload).catch(() => {
        // Ignore dismissed share sheets.
      });
    },
    [resolveSlideDownloadUrl],
  );

  return { handleCustomDownload, handleCustomShare };
};
