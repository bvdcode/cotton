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
import {
  convertHeicToJpeg,
  isHeicFile,
} from "../../../shared/utils/heicConverter";
import { formatBytes } from "../../../shared/utils/formatBytes";
import { shareLinks } from "../../../shared/utils/shareLinks";

const TRANSPARENT_PLACEHOLDER =
  "data:image/gif;base64,R0lGODlhAQABAAAAACH5BAEKAAEALAAAAAABAAEAAAICTAEAOw==";

const LIGHTBOX_ANIMATION_MS = 200;
const LIGHTBOX_PREFETCH_OFFSETS: ReadonlyArray<number> = [-1, 0, 1];
const TOUCH_CONTROLS_AUTOHIDE_MS = 2500;

type SlideWithTitle = Slide & {
  fileId: string;
  fileName: string;
  title?: string;
};

type MediaKind = "image" | "video";

export interface MediaItem {
  id: string;
  kind: MediaKind;
  name: string;
  previewUrl: string;
  width?: number;
  height?: number;
  mimeType: string;
  sizeBytes?: number;
}

export interface MediaLightboxProps {
  items: MediaItem[];
  open: boolean;
  initialIndex: number;
  onClose: () => void;
  getSignedMediaUrl: (id: string) => Promise<string>;
  /**
   * If false, disables swipe/fade animations.
   * Defaults to true.
   */
  smoothTransitions?: boolean;
  /**
   * Optional separate download URL resolver.
   * If not provided, the signed media URL is used for both viewing and downloading.
   */
  getDownloadUrl?: (id: string) => Promise<string>;
}

/**
 * MediaLightbox component
 * Displays media items (images/videos) as slides with lazy-loaded signed URLs
 */
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

  // Auto-hide controls after 2.5 seconds of inactivity
  const isActive = useActivityDetection(TOUCH_CONTROLS_AUTOHIDE_MS);

  const [touchControlsVisible, setTouchControlsVisible] =
    React.useState<boolean>(true);

  const touchControlsTimerRef = React.useRef<number | null>(null);

  const showTouchControls = React.useCallback(() => {
    if (!isTouchDevice) {
      return;
    }

    setTouchControlsVisible(true);

    if (touchControlsTimerRef.current !== null) {
      window.clearTimeout(touchControlsTimerRef.current);
      touchControlsTimerRef.current = null;
    }

    touchControlsTimerRef.current = window.setTimeout(() => {
      setTouchControlsVisible(false);
      touchControlsTimerRef.current = null;
    }, TOUCH_CONTROLS_AUTOHIDE_MS);
  }, [isTouchDevice]);

  React.useEffect(() => {
    if (!open) {
      return;
    }

    if (!isTouchDevice) {
      return;
    }

    showTouchControls();

    return () => {
      if (touchControlsTimerRef.current !== null) {
        window.clearTimeout(touchControlsTimerRef.current);
        touchControlsTimerRef.current = null;
      }
    };
  }, [open, isTouchDevice, showTouchControls]);

  const [signedUrls, setSignedUrls] = React.useState<Record<string, string>>(
    {},
  );
  const [displayUrls, setDisplayUrls] = React.useState<Record<string, string>>(
    {},
  );
  const [downloadUrls, setDownloadUrls] = React.useState<
    Record<string, string>
  >({});
  const downloadUrlsRef = React.useRef<Record<string, string>>({});
  const inFlightDownloadLoadsRef = React.useRef<Map<string, Promise<string | null>>>(
    new Map(),
  );
  const loadingRef = React.useRef<Set<string>>(new Set());

  React.useEffect(() => {
    downloadUrlsRef.current = downloadUrls;
  }, [downloadUrls]);

  const slides = React.useMemo(() => {
    return buildSlidesFromItems(items, displayUrls, signedUrls);
  }, [items, displayUrls, signedUrls]);

  const preloadImage = React.useCallback(async (url: string): Promise<void> => {
    await new Promise<void>((resolve, reject) => {
      const image = new Image();

      image.onload = async () => {
        if (typeof image.decode === "function") {
          try {
            await image.decode();
          } catch {
            // ignore decode failures
          }
        }

        resolve();
      };

      image.onerror = () => reject(new Error("Failed to preload image"));
      image.src = url;
    });
  }, []);

  const ensureSlideHasOriginal = React.useCallback(
    async (targetIndex: number) => {
      const item = items[targetIndex];
      if (!item) return;

      if (loadingRef.current.has(item.id)) {
        return;
      }

      setSignedUrls((previous) => {
        if (previous[item.id]) {
          return previous;
        }

        loadingRef.current.add(item.id);

        void (async () => {
          try {
            const signedUrl = await getSignedMediaUrl(item.id);
            setSignedUrls((nextPrevious) => ({ ...nextPrevious, [item.id]: signedUrl }));

            const nextDisplayUrl =
              item.kind === "image" && isHeicFile(item.name)
                ? await convertHeicToJpeg(signedUrl)
                : signedUrl;

            if (item.kind === "image") {
              try {
                await preloadImage(nextDisplayUrl);
              } catch {
                // Keep previewUrl if preloading fails.
              }
            }

            setDisplayUrls((nextPrevious) => ({ ...nextPrevious, [item.id]: nextDisplayUrl }));
          } catch (error) {
            console.error("Failed to load media original URL", error);
          } finally {
            loadingRef.current.delete(item.id);
          }
        })();

        return previous;
      });
    },
    [items, getSignedMediaUrl, preloadImage],
  );

  const ensureDownloadUrl = React.useCallback(
    async (fileId: string): Promise<string | null> => {
      const existingUrl = downloadUrlsRef.current[fileId];
      if (existingUrl) {
        return existingUrl;
      }

      if (!getDownloadUrl) {
        return null;
      }

      const existingInFlight = inFlightDownloadLoadsRef.current.get(fileId);
      if (existingInFlight) {
        return await existingInFlight;
      }

      const loadTask = (async () => {
        try {
          const nextUrl = await getDownloadUrl(fileId);
          setDownloadUrls((previous) =>
            previous[fileId] ? previous : { ...previous, [fileId]: nextUrl },
          );
          return nextUrl;
        } catch (error) {
          console.error("Failed to load media download URL", error);
          return null;
        } finally {
          inFlightDownloadLoadsRef.current.delete(fileId);
        }
      })();

      inFlightDownloadLoadsRef.current.set(fileId, loadTask);
      return await loadTask;
    },
    [getDownloadUrl],
  );

  const getSlideSourceUrl = React.useCallback((slide: SlideWithTitle): string | null => {
    if (slide.type === "video") {
      const videoSlide = slide as SlideWithTitle & {
        sources?: Array<{ src?: string }>;
      };
      return videoSlide.sources?.[0]?.src ?? null;
    }

    const imageSlide = slide as SlideWithTitle & { src?: string };
    return imageSlide.src ?? null;
  }, []);

  const handleCustomDownload = React.useCallback(
    async ({
      slide,
      saveAs,
    }: {
      slide: Slide;
      saveAs: (source: string | Blob, name?: string) => void;
    }) => {
      const lightboxSlide = slide as SlideWithTitle;
      const downloadUrl =
        (await ensureDownloadUrl(lightboxSlide.fileId)) ??
        getSlideSourceUrl(lightboxSlide);

      if (!downloadUrl) {
        return;
      }

      saveAs(downloadUrl, lightboxSlide.fileName);
    },
    [ensureDownloadUrl, getSlideSourceUrl],
  );

  const handleCustomShare = React.useCallback(
    async ({ slide }: { slide: Slide }) => {
      const lightboxSlide = slide as SlideWithTitle;
      if (!navigator.canShare) {
        return;
      }

      const downloadUrl =
        (await ensureDownloadUrl(lightboxSlide.fileId)) ??
        getSlideSourceUrl(lightboxSlide);
      if (!downloadUrl) {
        return;
      }

      const token = shareLinks.tryExtractTokenFromDownloadUrl(downloadUrl);
      const shareUrl = token ? shareLinks.buildShareUrl(token) : downloadUrl;
      const sharePayload = { title: lightboxSlide.fileName, url: shareUrl };

      if (!navigator.canShare(sharePayload)) {
        return;
      }

      navigator.share(sharePayload).catch(() => {
        // Ignore dismissed share sheets.
      });
    },
    [ensureDownloadUrl, getSlideSourceUrl],
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

  // Build className based on activity state
  const controlsVisible = isTouchDevice ? touchControlsVisible : isActive;
  const lightboxClassName = [
    "lightbox-autohide",
    controlsVisible ? "lightbox-autohide--active" : "lightbox-autohide--idle",
  ].join(" ");

  return (
    <Lightbox
      open={open}
      close={onClose}
      className={lightboxClassName}
      plugins={plugins}
      slides={slides}
      index={index}
      controller={{
        closeOnPullDown: true,
        closeOnPullUp: true,
      }}
      animation={{
        swipe: smoothTransitions ? LIGHTBOX_ANIMATION_MS : 0,
        fade: smoothTransitions ? LIGHTBOX_ANIMATION_MS : 0,
        navigation: smoothTransitions ? LIGHTBOX_ANIMATION_MS : 0,
      }}
      on={{
        view: ({ index: currentIndex }) => {
          setIndex(currentIndex);
          showTouchControls();
          for (const offset of LIGHTBOX_PREFETCH_OFFSETS) {
            void ensureSlideHasOriginal(currentIndex + offset);
          }
        },
        click: () => {
          if (!isTouchDevice) {
            return;
          }

          // Mobile UX: a tap should reliably show the gallery overlay.
          // We intentionally do NOT toggle off on tap to avoid interfering with video controls.
          showTouchControls();
        },
      }}
      render={{
        buttonZoom: () => null,
        iconZoomIn: () => null,
        iconZoomOut: () => null,
        iconLoading: () => <CircularProgress size={28} />,
        iconClose: () => <Close />,
        iconShare: () => <ShareIcon />,
        iconDownload: () => <DownloadIcon />,
        iconSlideshowPause: () => <PauseIcon />,
        iconSlideshowPlay: () => <SlideshowIcon />,
        slideHeader: ({ slide }) => {
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
        slideContainer: ({ children, slide }) => {
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
      }}
      download={{
        download: handleCustomDownload,
      }}
      share={{
        share: handleCustomShare,
      }}
      zoom={{
        maxZoomPixelRatio: 3,
        zoomInMultiplier: 1,
        doubleTapDelay: 300,
        doubleClickDelay: 300,
        doubleClickMaxStops: 1,
        keyboardMoveDistance: 50,
        wheelZoomDistanceFactor: 500,
        pinchZoomDistanceFactor: 100,
        scrollToZoom: true,
      }}
      slideshow={{
        autoplay: false,
        delay: 5000,
      }}
      thumbnails={
        isTouchDevice
          ? undefined
          : {
              position: "bottom",
              width: 120,
              height: 80,
              border: 0,
              borderRadius: 4,
              padding: 2,
              gap: 4,
              showToggle: false,
              hidden: items[index]?.kind === "video",
            }
      }
      video={{
        controls: true,
        playsInline: true,
        autoPlay: true,
      }}
      carousel={{
        finite: true,
        preload: 2,
        imageFit: "contain",
        padding: 0,
        spacing: 0,
      }}
    />
  );
};

function buildSlidesFromItems(
  items: MediaItem[],
  displayUrls: Record<string, string>,
  signedUrls: Record<string, string>,
): SlideWithTitle[] {
  const total = items.length;

  const buildTitle = (position: number, item: MediaItem): string => {
    const prefix = total > 0 ? `${position}/${total}` : "";
    const sizeStr = item.sizeBytes ? formatBytes(item.sizeBytes) : "";
    return sizeStr
      ? `${prefix} • ${item.name} • ${sizeStr}`
      : `${prefix} • ${item.name}`;
  };

  const buildImageSlide = (args: {
    item: MediaItem;
    title: string;
    displayUrl: string | null;
  }): SlideWithTitle => {
    const { item, title, displayUrl } = args;
    const isLoading = !displayUrl && !item.previewUrl;
    const src = displayUrl || item.previewUrl || TRANSPARENT_PLACEHOLDER;

    return {
      fileId: item.id,
      fileName: item.name,
      type: "image",
      src,
      width: isLoading ? 120 : item.width,
      height: isLoading ? 120 : item.height,
      title,
      download: true,
      share: true,
    };
  };

  const buildVideoSlide = (args: {
    item: MediaItem;
    title: string;
    signedUrl: string | null;
  }): SlideWithTitle => {
    const { item, title, signedUrl } = args;
    const poster = item.previewUrl || undefined;

    if (!signedUrl) {
      return {
        fileId: item.id,
        fileName: item.name,
        type: "image",
        src: poster || TRANSPARENT_PLACEHOLDER,
        width: item.width,
        height: item.height,
        title,
        download: true,
        share: true,
      };
    }

    return {
      fileId: item.id,
      fileName: item.name,
      type: "video",
      poster,
      width: item.width,
      height: item.height,
      title,
      download: true,
      share: true,
      sources: [
        {
          src: signedUrl,
          type: item.mimeType,
        },
      ],
    } as SlideWithTitle;
  };

  return items.map<SlideWithTitle>((item, idx) => {
    const position = idx + 1;
    const title = buildTitle(position, item);

    const signedUrl = signedUrls[item.id] ?? null;
    const displayUrl = displayUrls[item.id] ?? null;

    if (item.kind === "image") {
      return buildImageSlide({
        item,
        title,
        displayUrl,
      });
    }

    return buildVideoSlide({
      item,
      title,
      signedUrl,
    });
  });
}
