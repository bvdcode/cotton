import React from "react";
import Lightbox from "yet-another-react-lightbox";
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
} from "@mui/icons-material";
import { CircularProgress } from "@mui/material";
import { useActivityDetection } from "../hooks/useActivityDetection";
import type {
  MediaLightboxProps,
  SlideWithTitle,
} from "./mediaLightbox.types";
import { useMediaLightboxUrls } from "../hooks/useMediaLightboxUrls";
import { shareLinks } from "../../../shared/utils/shareLinks";

const LIGHTBOX_ANIMATION_MS = 200;
const LIGHTBOX_PREFETCH_OFFSETS: ReadonlyArray<number> = [-1, 0, 1];
const TOUCH_CONTROLS_AUTOHIDE_MS = 2500;

export const MediaLightbox: React.FC<MediaLightboxProps> = ({
  items,
  open,
  initialIndex,
  onClose,
  getSignedMediaUrl,
  smoothTransitions = true,
  getDownloadUrl,
}) => {
  const [index, setIndex] = React.useState(initialIndex);

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

    showTouchControls();

    return () => {
      clearTouchControlsTimer();
    };
  }, [open, isTouchDevice, showTouchControls, clearTouchControlsTimer]);

  const {
    slides,
    ensureSlideHasOriginal,
    resolveSlideDownloadUrl,
  } = useMediaLightboxUrls({
    items,
    getSignedMediaUrl,
    getDownloadUrl,
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
    setIndex(initialIndex);
  }, [initialIndex]);

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
        setIndex(currentIndex);
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
    }),
    [ensureSlideHasOriginal, isTouchDevice, showTouchControls, toggleTouchControls],
  );

  const lightboxRender = React.useMemo(
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
      slideHeader: ({ slide }: { slide: Slide }) => {
        const maybeTitle = (slide as { title?: string }).title;
        const title = typeof maybeTitle === "string" ? maybeTitle : "";
        const parts = title
          .split("•")
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
                  <span className="media-lightbox__sep">•</span>
                  <span className="media-lightbox__size">{size}</span>
                </>
              ) : null}
              {name ? (
                <>
                  <span className="media-lightbox__sep">•</span>
                  <span className="media-lightbox__name">{name}</span>
                </>
              ) : null}
            </span>
          </div>
        );
      },
      slideContainer: ({
        children,
        slide,
      }: {
        children?: React.ReactNode;
        slide: Slide;
      }) => {
        const imageSrc =
          slide.type === "image" ? (slide as { src?: string }).src : null;

        return (
          <div className="media-lightbox__tap-area">
            {imageSrc ? (
              <img
                className="media-lightbox__image-bg"
                src={imageSrc}
                alt=""
                aria-hidden="true"
              />
            ) : null}
            {children}
          </div>
        );
      },
    }),
    [],
  );

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

  const lightboxVideo = React.useMemo(
    () => ({
      controls: true,
      playsInline: true,
      autoPlay: true,
    }),
    [],
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

  const lightboxThumbnails = React.useMemo(() => {
    if (isTouchDevice) {
      return undefined;
    }

    return {
      position: "bottom" as const,
      width: 120,
      height: 80,
      border: 0,
      borderRadius: 4,
      padding: 2,
      gap: 4,
      showToggle: false,
      hidden: items[index]?.kind === "video",
    };
  }, [index, isTouchDevice, items]);

  return (
    <Lightbox
      open={open}
      close={onClose}
      className={lightboxClassName}
      plugins={plugins}
      slides={slides}
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
      video={lightboxVideo}
      carousel={lightboxCarousel}
    />
  );
};
