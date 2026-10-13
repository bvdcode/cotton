import React from "react";
import { Box } from "@mui/material";
import type { Slide } from "yet-another-react-lightbox";
import type {
  SlideHlsVideo,
  SlideWithTitle,
} from "@shared/types/mediaLightbox";
import {
  HLS_VIDEO_SLIDE_TYPE,
  isSlideWithTitle,
} from "@shared/types/mediaLightbox";
import { HlsVideoSlide } from "./HlsVideoSlide";
import { darkTheme } from "../../theme";
import { formatBytes } from "../../utils/formatBytes";

type SetActiveVideoElementForFile = (
  fileId: string,
  element: HTMLVideoElement | null,
) => void;
const LIGHTBOX_TITLE_SEPARATOR = "\u2022";

type HlsVideoLightboxSlideProps = {
  currentItemId: string | null;
  errorText: string;
  noticeText: string;
  offset: number;
  setActiveVideoElementForFile: SetActiveVideoElementForFile;
  slide: Slide;
};

export const HlsVideoLightboxSlide = ({
  currentItemId,
  errorText,
  noticeText,
  offset,
  setActiveVideoElementForFile,
  slide,
}: HlsVideoLightboxSlideProps) => {
  if (slide.type !== HLS_VIDEO_SLIDE_TYPE || !isSlideWithTitle(slide)) {
    return undefined;
  }

  const hlsSlide: SlideHlsVideo & SlideWithTitle = slide;
  return (
    <HlsVideoSlide
      src={hlsSlide.src}
      poster={hlsSlide.poster}
      width={hlsSlide.width}
      height={hlsSlide.height}
      active={offset === 0 && hlsSlide.fileId === currentItemId}
      onVideoElementChange={(element) =>
        setActiveVideoElementForFile(hlsSlide.fileId, element)
      }
      noticeText={noticeText}
      errorText={errorText}
    />
  );
};

export const MediaLightboxSlideHeader = ({ slide }: { slide: Slide }) => {
  const source = isSlideWithTitle(slide) ? slide : undefined;
  const maybeTitle = isSlideWithTitle(slide) ? slide.title : undefined;
  const title = typeof maybeTitle === "string" ? maybeTitle : "";
  const parts = title
    .split(LIGHTBOX_TITLE_SEPARATOR)
    .map((p: string) => p.trim())
    .filter((p: string) => p.length > 0);

  const counter = parts[0] ?? "";
  const size =
    source?.sizeBytes !== undefined ? formatBytes(source.sizeBytes) : "";
  const name = source?.fileName ?? "";

  return (
    <Box
      className="media-lightbox__header"
      aria-label={title}
      sx={{ color: darkTheme.palette.text.primary }}
    >
      <span className="media-lightbox__counter">{counter}</span>
      <span className="media-lightbox__meta">
        {size ? (
          <>
            <span className="media-lightbox__sep">
              {LIGHTBOX_TITLE_SEPARATOR}
            </span>
            <span className="media-lightbox__size">{size}</span>
          </>
        ) : null}
        {name ? (
          <>
            <span className="media-lightbox__sep">
              {LIGHTBOX_TITLE_SEPARATOR}
            </span>
            <span className="media-lightbox__name">{name}</span>
          </>
        ) : null}
      </span>
    </Box>
  );
};

type MediaLightboxSlideContainerProps = {
  children?: React.ReactNode;
  currentItemId: string | null;
  handleSlideImageError: (slide: Slide) => void;
  setActiveVideoElementForFile: SetActiveVideoElementForFile;
  slide: Slide;
};

export const MediaLightboxSlideContainer = ({
  children,
  currentItemId,
  handleSlideImageError,
  setActiveVideoElementForFile,
  slide,
}: MediaLightboxSlideContainerProps) => {
  const lightboxSlide = slide as Partial<SlideWithTitle>;
  const fileId =
    typeof lightboxSlide.fileId === "string" ? lightboxSlide.fileId : null;
  const previewUrl =
    slide.type === "image"
      ? (slide as { thumbnail?: string }).thumbnail
      : undefined;

  return (
    <div
      className="media-lightbox__tap-area"
      data-cotton-media-lightbox-file-id={fileId ?? undefined}
      onPlayCapture={(event) => {
        const target = event.target;
        if (
          fileId &&
          fileId === currentItemId &&
          target instanceof HTMLVideoElement
        ) {
          setActiveVideoElementForFile(fileId, target);
        }
      }}
      onErrorCapture={() => {
        void handleSlideImageError(slide);
      }}
    >
      {previewUrl && (
        <img
          src={previewUrl}
          alt=""
          aria-hidden
          draggable={false}
          className="media-lightbox__preview-bg"
        />
      )}
      {children}
    </div>
  );
};
