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
import { DeleteOutline as DeleteIcon } from "@mui/icons-material";
import type { MediaLightboxProps } from "@shared/types/mediaLightbox";
import { useMediaLightboxUrls } from "./useMediaLightboxUrls";
import { stopLightboxMediaPlayback } from "./mediaLightboxPlayback";
import { useMediaSessionSource } from "../../hooks/useMediaSessionSource";
import { MEDIA_SESSION_SOURCE_PRIORITY } from "../../utils/mediaSessionCoordinator";
import { buildVideoMediaSessionTrack } from "../../utils/mediaSessionTrack";
import {
  selectGalleryPreferPreview,
  useUserPreferencesStore,
} from "../../store/userPreferencesStore";

const LIGHTBOX_ANIMATION_MS = 200;
const TOUCH_CONTROLS_AUTOHIDE_MS = 2500;
const LIGHTBOX_PREFETCH_OFFSETS: ReadonlyArray<number> = [-1, 0, 1];
import { useLightboxIndex, useActiveVideoElement } from "./useLightboxState";
import { useMediaLightboxRender } from "./mediaLightboxRender";
import { useLightboxTouchControls } from "./useLightboxTouchControls";
import { useLightboxSharing } from "./useLightboxSharing";
import { useActivityDetection } from "../../hooks/useActivityDetection";

type ClosingState = { open: boolean; closing: boolean };
type DeleteProgressState = { itemId: string | null; inProgress: boolean };

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
  const { index, indexKey, setLightboxIndex } = useLightboxIndex(
    open,
    initialIndex,
    items,
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
  const {
    activeVideoElement,
    setActiveVideoElementForFile,
    setActiveVideoState,
  } = useActiveVideoElement(indexKey, currentItemId);

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
  const {
    touchControlsVisible,
    showTouchControls,
    toggleTouchControls,
    setTouchControlsVisible,
  } = useLightboxTouchControls(open, isTouchDevice);
  const [closingState, setClosingState] = React.useState<ClosingState>(() => ({
    open,
    closing: false,
  }));
  let isClosing = closingState.open === open ? closingState.closing : false;
  if (closingState.open !== open) {
    isClosing = false;
    setClosingState({ open, closing: false });
  }
  const handleClose = React.useCallback(() => {
    setClosingState({ open, closing: true });
    stopLightboxMediaPlayback();
    setActiveVideoState(null);
    setTouchControlsVisible(true);
    onClose();
  }, [onClose, open, setActiveVideoState, setTouchControlsVisible]);

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

  const { handleCustomDownload, handleCustomShare } = useLightboxSharing(
    resolveSlideDownloadUrl,
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
