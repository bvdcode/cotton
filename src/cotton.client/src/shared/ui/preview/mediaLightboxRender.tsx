import React from "react";
import type { Slide } from "yet-another-react-lightbox";
import {
  Close,
  Share as ShareIcon,
  Pause as PauseIcon,
  Download as DownloadIcon,
  Slideshow as SlideshowIcon,
} from "@mui/icons-material";
import { CircularProgress } from "@mui/material";
import { HLS_VIDEO_SLIDE_TYPE } from "@shared/types/mediaLightbox";

type SetActiveVideoElementForFile = (
  fileId: string,
  element: HTMLVideoElement | null,
) => void;

import {
  HlsVideoLightboxSlide,
  MediaLightboxSlideHeader,
  MediaLightboxSlideContainer,
} from "./MediaLightboxSlideComponents";

type UseMediaLightboxRenderOptions = {
  currentItemId: string | null;
  handleSlideImageError: (slide: Slide) => void;
  hlsErrorText: string;
  hlsNoticeText: string;
  setActiveVideoElementForFile: SetActiveVideoElementForFile;
};

export const useMediaLightboxRender = ({
  currentItemId,
  handleSlideImageError,
  hlsErrorText,
  hlsNoticeText,
  setActiveVideoElementForFile,
}: UseMediaLightboxRenderOptions) =>
  React.useMemo(
    () => ({
      buttonZoom: () => null,
      iconZoomIn: () => null,
      iconZoomOut: () => null,
      iconLoading: () => <CircularProgress size={28} />,
      iconClose: () => <Close />,
      iconShare: () => <ShareIcon />,
      iconDownload: () => <DownloadIcon />,
      iconSlideshowPause: () => <PauseIcon />,
      iconSlideshowPlay: () => <SlideshowIcon />,
      slide: ({ slide, offset }: { slide: Slide; offset: number }) =>
        slide.type === HLS_VIDEO_SLIDE_TYPE ? (
          <HlsVideoLightboxSlide
            currentItemId={currentItemId}
            errorText={hlsErrorText}
            noticeText={hlsNoticeText}
            offset={offset}
            setActiveVideoElementForFile={setActiveVideoElementForFile}
            slide={slide}
          />
        ) : undefined,
      slideHeader: ({ slide }: { slide: Slide }) => (
        <MediaLightboxSlideHeader slide={slide} />
      ),
      slideContainer: ({
        children,
        slide,
      }: {
        children?: React.ReactNode;
        slide: Slide;
      }) => (
        <MediaLightboxSlideContainer
          currentItemId={currentItemId}
          handleSlideImageError={handleSlideImageError}
          setActiveVideoElementForFile={setActiveVideoElementForFile}
          slide={slide}
        >
          {children}
        </MediaLightboxSlideContainer>
      ),
    }),
    [
      currentItemId,
      handleSlideImageError,
      hlsErrorText,
      hlsNoticeText,
      setActiveVideoElementForFile,
    ],
  );
