import React from "react";
import { useTranslation } from "react-i18next";
import Lightbox, { IconButton } from "yet-another-react-lightbox";
import "yet-another-react-lightbox/styles.css";
import "./MediaLightbox.css";
import Video from "yet-another-react-lightbox/plugins/video";
import Download from "yet-another-react-lightbox/plugins/download";
import Zoom from "yet-another-react-lightbox/plugins/zoom";
import Slideshow from "yet-another-react-lightbox/plugins/slideshow";
import Thumbnails from "yet-another-react-lightbox/plugins/thumbnails";
import Share from "yet-another-react-lightbox/plugins/share";
import "yet-another-react-lightbox/plugins/thumbnails.css";
import type { Slide } from "yet-another-react-lightbox";
import {
  Close,
  Share as ShareIcon,
  Pause as PauseIcon,
  Download as DownloadIcon,
  Slideshow as SlideshowIcon,
  DeleteOutline as DeleteIcon,
} from "@mui/icons-material";
import { CircularProgress } from "@mui/material";
import { useActivityDetection } from "../../hooks/useActivityDetection";
import type {
  MediaLightboxProps,
  SlideHlsVideo,
  SlideWithTitle,
} from "@shared/types/mediaLightbox";
import { HLS_VIDEO_SLIDE_TYPE } from "@shared/types/mediaLightbox";
import { useMediaLightboxUrls } from "./useMediaLightboxUrls";
import { stopLightboxMediaPlayback } from "./mediaLightboxPlayback";
import { useMediaSessionSource } from "../../hooks/useMediaSessionSource";
import { MEDIA_SESSION_SOURCE_PRIORITY } from "../../utils/mediaSessionCoordinator";
import { buildVideoMediaSessionTrack } from "../../utils/mediaSessionTrack";
import { shareLinks } from "../../utils/shareLinks";
import {
  selectGalleryPreferPreview,
  useUserPreferencesStore,
} from "../../store/userPreferencesStore";

const LIGHTBOX_ANIMATION_MS = 200;
const LIGHTBOX_PREFETCH_OFFSETS: ReadonlyArray<number> = [-1, 0, 1];
const TOUCH_CONTROLS_AUTOHIDE_MS = 2500;
const LIGHTBOX_TITLE_SEPARATOR = "\u2022";
const HlsVideoSlide = React.lazy(async () => {
  const module = await import("./HlsVideoSlide");
  return { default: module.HlsVideoSlide };
});

type LightboxIndexState = {
  key: string;
  index: number;
};

type ClosingState = {
  open: boolean;
  closing: boolean;
};

type IndexOrUpdater = number | ((current: number) => number);

type DeleteProgressState = {
  itemId: string | null;
  inProgress: boolean;
};

type ActiveVideoState = {
  key: string;
  fileId: string;
  element: HTMLVideoElement;
};

type SetActiveVideoElementForFile = (
  fileId: string,
  element: HTMLVideoElement | null,
) => void;

const buildLightboxIndexKey = (
  open: boolean,
  initialIndex: number,
  items: MediaLightboxProps["items"],
): string => {
  if (!open) {
    return "closed";
  }

  return [initialIndex, items[initialIndex]?.id ?? ""].join("\u0000");
};

const resolveIndex = (current: number, next: IndexOrUpdater): number => {
  return typeof next === "function" ? next(current) : next;
};

type HlsVideoLightboxSlideProps = {
  currentItemId: string | null;
  errorText: string;
  noticeText: string;
  offset: number;
  setActiveVideoElementForFile: SetActiveVideoElementForFile;
  slide: Slide;
};

const HlsVideoLightboxSlide = ({
  currentItemId,
  errorText,
  noticeText,
  offset,
  setActiveVideoElementForFile,
  slide,
}: HlsVideoLightboxSlideProps) => {
  if (slide.type !== HLS_VIDEO_SLIDE_TYPE) {
    return undefined;
  }

  const hlsSlide = slide as SlideHlsVideo & SlideWithTitle;
  return (
    <React.Suspense fallback={null}>
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
    </React.Suspense>
  );
};

const MediaLightboxSlideHeader = ({ slide }: { slide: Slide }) => {
  const maybeTitle = (slide as { title?: string }).title;
  const title = typeof maybeTitle === "string" ? maybeTitle : "";
  const parts = title
    .split(LIGHTBOX_TITLE_SEPARATOR)
    .map((p: string) => p.trim())
    .filter((p: string) => p.length > 0);

  const counter = parts[0] ?? "";
  const size = parts.length >= 3 ? (parts[1] ?? "") : "";
  const name = parts.length >= 2 ? (parts[parts.length - 1] ?? "") : "";

  return (
    <div className="media-lightbox__header" aria-label={title}>
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
    </div>
  );
};

type MediaLightboxSlideContainerProps = {
  children?: React.ReactNode;
  currentItemId: string | null;
  handleSlideImageError: (slide: Slide) => void;
  setActiveVideoElementForFile: SetActiveVideoElementForFile;
  slide: Slide;
};

const MediaLightboxSlideContainer = ({
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

type UseMediaLightboxRenderOptions = {
  currentItemId: string | null;
  handleSlideImageError: (slide: Slide) => void;
  hlsErrorText: string;
  hlsNoticeText: string;
  setActiveVideoElementForFile: SetActiveVideoElementForFile;
};

const useMediaLightboxRender = ({
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

export const MediaLightbox: React.FC<MediaLightboxProps> = ({
  items,
  open,
  initialIndex,
  onClose,
  getSignedMediaUrl,
  smoothTransitions = true,
  getDownloadUrl,
  onDelete,
}) => {
  const { t } = useTranslation(["files", "common"]);
  const indexKey = buildLightboxIndexKey(open, initialIndex, items);
  const [indexState, setIndexState] = React.useState<LightboxIndexState>(
    () => ({ key: indexKey, index: initialIndex }),
  );
  const rawIndex =
    indexState.key === indexKey ? indexState.index : initialIndex;
  const index = items.length === 0 ? 0 : Math.min(rawIndex, items.length - 1);
  const setLightboxIndex = React.useCallback(
    (next: IndexOrUpdater) => {
      setIndexState((currentState) => {
        const current =
          currentState.key === indexKey
            ? currentState
            : { key: indexKey, index: initialIndex };
        const nextIndex = resolveIndex(current.index, next);
        if (nextIndex === current.index) {
          return currentState.key === indexKey ? currentState : current;
        }

        return { key: indexKey, index: nextIndex };
      });
    },
    [indexKey, initialIndex],
  );
  const hlsNoticeText = t("preview.video.transcodeNotice");
  const hlsErrorText = t("preview.video.transcodeError");
  const preferPreview = useUserPreferencesStore(selectGalleryPreferPreview);
  const currentItemId = React.useMemo(
    () => (open ? (items[index]?.id ?? null) : null),
    [index, items, open],
  );
  const currentItem = open ? items[index] : undefined;
  const currentIsVideo = currentItem?.kind === "video";
  const [activeVideoState, setActiveVideoState] =
    React.useState<ActiveVideoState | null>(null);
  const activeVideoElement =
    activeVideoState?.key === indexKey &&
    activeVideoState.fileId === currentItemId
      ? activeVideoState.element
      : null;

  const setActiveVideoElementForFile = React.useCallback(
    (fileId: string, element: HTMLVideoElement | null) => {
      setActiveVideoState((current) => {
        if (!element) {
          return current?.key === indexKey && current.fileId === fileId
            ? null
            : current;
        }
        return { key: indexKey, fileId, element };
      });
    },
    [indexKey],
  );

  const videoMediaSessionTrack = React.useMemo(
    () =>
      currentItem?.kind === "video"
        ? buildVideoMediaSessionTrack(currentItem)
        : null,
    [currentItem],
  );

  const hasMultipleItems = items.length > 1;
  const handlePreviousMedia = React.useCallback(() => {
    setLightboxIndex((current) => Math.max(0, current - 1));
  }, [setLightboxIndex]);
  const handleNextMedia = React.useCallback(() => {
    setLightboxIndex((current) => Math.min(items.length - 1, current + 1));
  }, [items.length, setLightboxIndex]);

  useMediaSessionSource({
    mediaElement: open && currentIsVideo ? activeVideoElement : null,
    track: videoMediaSessionTrack,
    priority: MEDIA_SESSION_SOURCE_PRIORITY.video,
    onPreviousTrack: hasMultipleItems ? handlePreviousMedia : undefined,
    onNextTrack: hasMultipleItems ? handleNextMedia : undefined,
  });

  const isTouchDevice = React.useMemo(() => {
    if (typeof window === "undefined") return false;
    return window.matchMedia?.("(hover: none)")?.matches ?? false;
  }, []);

  const plugins = React.useMemo(
    () =>
      isTouchDevice
        ? [Video, Zoom, Slideshow, Download, Share]
        : [Video, Zoom, Slideshow, Thumbnails, Download, Share],
    [isTouchDevice],
  );

  const isActive = useActivityDetection(TOUCH_CONTROLS_AUTOHIDE_MS);
  const [touchControlsVisible, setTouchControlsVisible] =
    React.useState<boolean>(true);
  const [closingState, setClosingState] = React.useState<ClosingState>(() => ({
    open,
    closing: false,
  }));
  let isClosing = closingState.open === open ? closingState.closing : false;
  if (closingState.open !== open) {
    isClosing = false;
    setClosingState({ open, closing: false });
  }
  const touchControlsTimerRef = React.useRef<number | null>(null);

  const clearTouchControlsTimer = React.useCallback(() => {
    if (touchControlsTimerRef.current !== null) {
      window.clearTimeout(touchControlsTimerRef.current);
      touchControlsTimerRef.current = null;
    }
  }, []);

  const showTouchControls = React.useCallback(() => {
    if (!isTouchDevice) return;

    setTouchControlsVisible(true);
    clearTouchControlsTimer();

    touchControlsTimerRef.current = window.setTimeout(() => {
      setTouchControlsVisible(false);
      touchControlsTimerRef.current = null;
    }, TOUCH_CONTROLS_AUTOHIDE_MS);
  }, [clearTouchControlsTimer, isTouchDevice]);

  const handleClose = React.useCallback(() => {
    setClosingState({ open, closing: true });
    stopLightboxMediaPlayback();
    setActiveVideoState(null);
    setTouchControlsVisible(true);
    onClose();
  }, [onClose, open]);

  const toggleTouchControls = React.useCallback(() => {
    if (!isTouchDevice) return;

    setTouchControlsVisible((previous) => {
      const next = !previous;
      clearTouchControlsTimer();

      if (next) {
        touchControlsTimerRef.current = window.setTimeout(() => {
          setTouchControlsVisible(false);
          touchControlsTimerRef.current = null;
        }, TOUCH_CONTROLS_AUTOHIDE_MS);
      }

      return next;
    });
  }, [clearTouchControlsTimer, isTouchDevice]);

  React.useEffect(() => {
    if (!open || !isTouchDevice) {
      return;
    }

    clearTouchControlsTimer();
    touchControlsTimerRef.current = window.setTimeout(() => {
      setTouchControlsVisible(false);
      touchControlsTimerRef.current = null;
    }, TOUCH_CONTROLS_AUTOHIDE_MS);

    return () => {
      clearTouchControlsTimer();
    };
  }, [open, isTouchDevice, clearTouchControlsTimer]);

  const [deleteProgress, setDeleteProgress] =
    React.useState<DeleteProgressState>(() => ({
      itemId: currentItemId,
      inProgress: false,
    }));
  const deleteInProgressRef = React.useRef<DeleteProgressState>({
    itemId: null,
    inProgress: false,
  });
  const deleteInProgress =
    deleteProgress.itemId === currentItemId && deleteProgress.inProgress;

  const handleDeleteCurrent = React.useCallback(async () => {
    if (
      !onDelete ||
      !currentItem ||
      (deleteInProgressRef.current.itemId === currentItemId &&
        deleteInProgressRef.current.inProgress)
    ) {
      return;
    }

    deleteInProgressRef.current = { itemId: currentItemId, inProgress: true };
    setDeleteProgress({ itemId: currentItemId, inProgress: true });
    try {
      await onDelete(currentItem);
      if (items.length <= 1) {
        handleClose();
      }
    } catch (error) {
      console.error("Failed to delete media item:", error);
    } finally {
      deleteInProgressRef.current = {
        itemId: currentItemId,
        inProgress: false,
      };
      setDeleteProgress({ itemId: currentItemId, inProgress: false });
    }
  }, [currentItem, currentItemId, handleClose, items.length, onDelete]);

  React.useEffect(() => {
    if (!open || !onDelete) {
      return;
    }

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.defaultPrevented || event.key !== "Delete") {
        return;
      }

      const target = event.target;
      if (
        target instanceof Element &&
        target.closest('input, textarea, [contenteditable="true"]')
      ) {
        return;
      }

      event.preventDefault();
      void handleDeleteCurrent();
    };

    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [handleDeleteCurrent, onDelete, open]);

  const {
    slides,
    ensureSlideHasOriginal,
    handleSlideImageError,
    resolveSlideDownloadUrl,
  } = useMediaLightboxUrls({
    items,
    getSignedMediaUrl,
    getDownloadUrl,
    preferPreview,
    currentItemId,
  });

  const handleCustomDownload = React.useCallback(
    async ({
      slide,
      saveAs,
    }: {
      slide: Slide;
      saveAs: (source: string | Blob, name?: string) => void;
    }) => {
      const lightboxSlide = slide as SlideWithTitle;
      const downloadUrl = await resolveSlideDownloadUrl(slide);
      if (!downloadUrl) return;
      saveAs(downloadUrl, lightboxSlide.fileName);
    },
    [resolveSlideDownloadUrl],
  );

  const handleCustomShare = React.useCallback(
    async ({ slide }: { slide: Slide }) => {
      const lightboxSlide = slide as SlideWithTitle;
      if (!navigator.canShare) return;

      const downloadUrl = await resolveSlideDownloadUrl(slide);
      if (!downloadUrl) return;

      const token = shareLinks.tryExtractTokenFromDownloadUrl(downloadUrl);
      const shareUrl = token ? shareLinks.buildShareUrl(token) : downloadUrl;
      const sharePayload = { title: lightboxSlide.fileName, url: shareUrl };

      if (!navigator.canShare(sharePayload)) return;

      navigator.share(sharePayload).catch(() => {
        // Ignore dismissed share sheets.
      });
    },
    [resolveSlideDownloadUrl],
  );

  React.useEffect(() => {
    if (!open) return;
    for (const offset of LIGHTBOX_PREFETCH_OFFSETS) {
      void ensureSlideHasOriginal(index + offset);
    }
  }, [open, index, ensureSlideHasOriginal]);

  const controlsVisible = isTouchDevice ? touchControlsVisible : isActive;
  const lightboxClassName = [
    "lightbox-autohide",
    controlsVisible ? "lightbox-autohide--active" : "lightbox-autohide--idle",
  ].join(" ");

  const lightboxController = React.useMemo(
    () => ({
      closeOnPullDown: true,
      closeOnPullUp: true,
    }),
    [],
  );

  const lightboxAnimation = React.useMemo(
    () => ({
      swipe: smoothTransitions ? LIGHTBOX_ANIMATION_MS : 0,
      fade: smoothTransitions ? LIGHTBOX_ANIMATION_MS : 0,
      navigation: smoothTransitions ? LIGHTBOX_ANIMATION_MS : 0,
    }),
    [smoothTransitions],
  );

  const lightboxEvents = React.useMemo(
    () => ({
      view: ({ index: currentIndex }: { index: number }) => {
        setLightboxIndex((previous) =>
          previous === currentIndex ? previous : currentIndex,
        );
        showTouchControls();
        for (const offset of LIGHTBOX_PREFETCH_OFFSETS) {
          void ensureSlideHasOriginal(currentIndex + offset);
        }
      },
      click: () => {
        if (!isTouchDevice) return;
        window.setTimeout(() => {
          toggleTouchControls();
        }, 0);
      },
      exiting: stopLightboxMediaPlayback,
      exited: stopLightboxMediaPlayback,
    }),
    [
      ensureSlideHasOriginal,
      isTouchDevice,
      setLightboxIndex,
      showTouchControls,
      toggleTouchControls,
    ],
  );

  const lightboxRender = useMediaLightboxRender({
    currentItemId,
    handleSlideImageError,
    hlsErrorText,
    hlsNoticeText,
    setActiveVideoElementForFile,
  });

  const lightboxDownload = React.useMemo(
    () => ({
      download: handleCustomDownload,
    }),
    [handleCustomDownload],
  );

  const lightboxShare = React.useMemo(
    () => ({
      share: handleCustomShare,
    }),
    [handleCustomShare],
  );

  const deleteButton = React.useMemo(() => {
    if (!onDelete || !currentItem) {
      return null;
    }

    return (
      <IconButton
        key="delete"
        label="Delete"
        icon={DeleteIcon}
        renderIcon={() => <DeleteIcon />}
        disabled={deleteInProgress}
        onClick={() => {
          void handleDeleteCurrent();
        }}
      />
    );
  }, [currentItem, deleteInProgress, handleDeleteCurrent, onDelete]);

  const lightboxLabels = React.useMemo(
    () => ({
      Delete: t("actions.delete", { ns: "common" }),
    }),
    [t],
  );

  const lightboxToolbar = React.useMemo(
    () => ({
      buttons: deleteButton
        ? ["slideshow", "download", deleteButton, "share", "close"]
        : ["slideshow", "download", "share", "close"],
    }),
    [deleteButton],
  );

  const lightboxZoom = React.useMemo(
    () => ({
      maxZoomPixelRatio: 3,
      zoomInMultiplier: 1,
      doubleTapDelay: 300,
      doubleClickDelay: 300,
      doubleClickMaxStops: 1,
      keyboardMoveDistance: 50,
      wheelZoomDistanceFactor: 500,
      pinchZoomDistanceFactor: 100,
      scrollToZoom: true,
    }),
    [],
  );

  const lightboxSlideshow = React.useMemo(
    () => ({
      autoplay: false,
      delay: 5000,
    }),
    [],
  );

  const mediaPlaybackActive = open && !isClosing;

  const lightboxVideo = React.useMemo(
    () => ({
      controls: true,
      playsInline: true,
      autoPlay: mediaPlaybackActive,
    }),
    [mediaPlaybackActive],
  );

  const lightboxCarousel = React.useMemo(
    () => ({
      finite: true,
      preload: 2,
      imageFit: "contain" as const,
      padding: 0,
      spacing: 0,
    }),
    [],
  );

  React.useEffect(() => {
    if (!open) {
      stopLightboxMediaPlayback();
    }

    return stopLightboxMediaPlayback;
  }, [open]);

  const lightboxThumbnails = React.useMemo(() => {
    if (isTouchDevice) {
      return undefined;
    }

    return {
      position: "bottom" as const,
      width: 120,
      height: 80,
      border: 1,
      borderRadius: 4,
      padding: 0,
      gap: 6,
      showToggle: false,
      hidden: items[index]?.kind === "video",
    };
  }, [index, isTouchDevice, items]);

  return (
    <Lightbox
      open={open}
      close={handleClose}
      className={lightboxClassName}
      plugins={plugins}
      slides={mediaPlaybackActive ? slides : []}
      index={index}
      controller={lightboxController}
      animation={lightboxAnimation}
      on={lightboxEvents}
      render={lightboxRender}
      download={lightboxDownload}
      share={lightboxShare}
      zoom={lightboxZoom}
      slideshow={lightboxSlideshow}
      thumbnails={lightboxThumbnails}
      toolbar={lightboxToolbar}
      labels={lightboxLabels}
      video={lightboxVideo}
      carousel={lightboxCarousel}
    />
  );
};
