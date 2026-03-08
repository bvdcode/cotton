import React from "react";
import {
  Box,
  CircularProgress,
  Dialog,
  Grow,
  IconButton,
  Snackbar,
  Tooltip,
  Typography,
} from "@mui/material";
import {
  ChevronLeft,
  ChevronRight,
  Close,
  Download as DownloadIcon,
  Share as ShareIcon,
} from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { useActivityDetection } from "../hooks/useActivityDetection";
import { formatBytes } from "../../../shared/utils/formatBytes";
import { shareLinkAction } from "../../../shared/utils/shareLinkAction";
import { shareLinks } from "../../../shared/utils/shareLinks";

const TRANSPARENT_PLACEHOLDER =
  "data:image/gif;base64,R0lGODlhAQABAAAAACH5BAEKAAEALAAAAAABAAEAAAICTAEAOw==";

const LIGHTBOX_ANIMATION_MS = 200;
const LIGHTBOX_PREFETCH_OFFSETS: ReadonlyArray<number> = [-1, 0, 1];
const TOUCH_CONTROLS_AUTOHIDE_MS = 2500;
const FEEDBACK_AUTOHIDE_MS = 2000;
const HORIZONTAL_SWIPE_PX = 56;
const VERTICAL_CLOSE_SWIPE_PX = 88;
const THUMBNAIL_REVEAL_ZONE_PX = 64;
const THUMBNAIL_WINDOW_RADIUS = 24;
const MAX_IMAGE_ZOOM = 5;
const DEFAULT_IMAGE_ZOOM = 2.5;
const ZOOM_WHEEL_FACTOR = 1.12;
const DOUBLE_TAP_DELAY_MS = 260;
const ZOOM_ANIMATE_MS = 250;
const BACKGROUND_CROSSFADE_MS = 320;

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
  smoothTransitions?: boolean;
  getDownloadUrl?: (id: string) => Promise<string>;
}

function clampIndex(index: number, total: number): number {
  if (total <= 0) {
    return 0;
  }

  return Math.min(Math.max(index, 0), total - 1);
}

function clampValue(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}

function buildShareUrl(candidateUrl: string | null): string | null {
  if (!candidateUrl) {
    return null;
  }

  const token = shareLinks.tryExtractTokenFromDownloadUrl(candidateUrl);
  return token ? shareLinks.buildShareUrl(token) : null;
}

function triggerDownload(url: string, fileName: string): void {
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  link.rel = "noopener noreferrer";
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
}

function buildMetaLabel(item: MediaItem | null, index: number, total: number): string {
  if (!item) {
    return "";
  }

  const counter = total > 0 ? `${index + 1}/${total}` : "";
  const size = item.sizeBytes ? formatBytes(item.sizeBytes) : "";

  return size ? `${counter} • ${item.name} • ${size}` : `${counter} • ${item.name}`;
}

function shouldIgnoreGestureTarget(target: EventTarget | null): boolean {
  if (!(target instanceof Element)) {
    return false;
  }

  return Boolean(target.closest("button, a, input, textarea, video, [role='button']"));
}

function getTouchDistance(
  firstTouch: { clientX: number; clientY: number },
  secondTouch: { clientX: number; clientY: number },
): number {
  return Math.hypot(firstTouch.clientX - secondTouch.clientX, firstTouch.clientY - secondTouch.clientY);
}

function stopEvent(event: { preventDefault?: () => void; stopPropagation: () => void }): void {
  event.preventDefault?.();
  event.stopPropagation();
}

interface TouchGestureState {
  startX: number;
  startY: number;
  lastX: number;
  lastY: number;
}

interface PinchGestureState {
  startDistance: number;
  startScale: number;
  startPanX: number;
  startPanY: number;
}

interface SlideTransitionState {
  fromIndex: number;
  toIndex: number;
  direction: 1 | -1;
  phase: "enter" | "active";
}

interface TapState {
  time: number;
  x: number;
  y: number;
}

/**
 * Internal media viewer used across files/search/share pages.
 * Replaces the previous third-party lightbox to avoid runtime crashes from plugin state loops.
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
  const { t } = useTranslation(["common", "share"]);
  const initialSlideIndex = React.useMemo(
    () => clampIndex(initialIndex, items.length),
    [initialIndex, items.length],
  );

  const [currentIndex, setCurrentIndex] = React.useState(initialSlideIndex);
  const [signedUrls, setSignedUrls] = React.useState<Record<string, string>>({});
  const signedUrlsRef = React.useRef<Record<string, string>>({});
  const [displayUrls, setDisplayUrls] = React.useState<Record<string, string>>({});
  const displayUrlsRef = React.useRef<Record<string, string>>({});
  const [downloadUrls, setDownloadUrls] = React.useState<Record<string, string>>({});
  const downloadUrlsRef = React.useRef<Record<string, string>>({});
  const inFlightLoadsRef = React.useRef<Map<string, Promise<void>>>(new Map());
  const [feedbackMessage, setFeedbackMessage] = React.useState("");
  const [feedbackOpen, setFeedbackOpen] = React.useState(false);
  const [touchControlsVisible, setTouchControlsVisible] = React.useState(true);
  const [isThumbnailStripHovered, setIsThumbnailStripHovered] = React.useState(false);
  const touchControlsTimerRef = React.useRef<number | null>(null);
  const touchGestureRef = React.useRef<TouchGestureState | null>(null);
  const pinchGestureRef = React.useRef<PinchGestureState | null>(null);
  const mediaRevealFrameRef = React.useRef<number | null>(null);
  const slideTransitionTimeoutRef = React.useRef<number | null>(null);
  const slideTransitionFrameRef = React.useRef<number | null>(null);
  const thumbnailStripRef = React.useRef<HTMLDivElement | null>(null);
  const thumbnailButtonRefs = React.useRef<Record<string, HTMLButtonElement | null>>({});
  const warmedImageUrlsRef = React.useRef<Set<string>>(new Set());
  const inFlightImagePreloadsRef = React.useRef<Map<string, Promise<boolean>>>(new Map());
  const mediaViewportRef = React.useRef<HTMLDivElement | null>(null);
  const lastTapRef = React.useRef<TapState | null>(null);
  const [mediaVisible, setMediaVisible] = React.useState(!smoothTransitions);
  const [slideTransition, setSlideTransition] = React.useState<SlideTransitionState | null>(null);
  const [zoom, setZoom] = React.useState({ scale: 1, panX: 0, panY: 0 });
  const zoomRef = React.useRef({ scale: 1, panX: 0, panY: 0 });
  const [zoomAnimating, setZoomAnimating] = React.useState(false);
  const zoomAnimateTimerRef = React.useRef<number | null>(null);
  const [isPanning, setIsPanning] = React.useState(false);
  const displayedBgRef = React.useRef(TRANSPARENT_PLACEHOLDER);
  const bgCrossfadeFrameRef = React.useRef<number | null>(null);
  const [bgLayers, setBgLayers] = React.useState({ bottom: TRANSPARENT_PLACEHOLDER, top: TRANSPARENT_PLACEHOLDER });
  const [bgTopOpacity, setBgTopOpacity] = React.useState(1);

  const isTouchDevice = React.useMemo(() => {
    if (typeof window === "undefined") {
      return false;
    }

    return window.matchMedia?.("(hover: none)")?.matches ?? false;
  }, []);
  const desktopControlsVisible = useActivityDetection(TOUCH_CONTROLS_AUTOHIDE_MS);

  React.useEffect(() => {
    signedUrlsRef.current = signedUrls;
  }, [signedUrls]);

  React.useEffect(() => {
    displayUrlsRef.current = displayUrls;
  }, [displayUrls]);

  React.useEffect(() => {
    downloadUrlsRef.current = downloadUrls;
  }, [downloadUrls]);

  const showTouchControls = React.useCallback(() => {
    if (!isTouchDevice || !open) {
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
  }, [isTouchDevice, open]);

  React.useEffect(() => {
    return () => {
      if (touchControlsTimerRef.current !== null) {
        window.clearTimeout(touchControlsTimerRef.current);
      }

      if (mediaRevealFrameRef.current !== null) {
        window.cancelAnimationFrame(mediaRevealFrameRef.current);
      }

      if (slideTransitionTimeoutRef.current !== null) {
        window.clearTimeout(slideTransitionTimeoutRef.current);
      }

      if (slideTransitionFrameRef.current !== null) {
        window.cancelAnimationFrame(slideTransitionFrameRef.current);
      }

      if (zoomAnimateTimerRef.current !== null) {
        window.clearTimeout(zoomAnimateTimerRef.current);
      }

      if (bgCrossfadeFrameRef.current !== null) {
        window.cancelAnimationFrame(bgCrossfadeFrameRef.current);
      }
    };
  }, []);

  React.useLayoutEffect(() => {
    if (!open) {
      return;
    }

    setCurrentIndex(initialSlideIndex);
    setTouchControlsVisible(true);
    showTouchControls();
  }, [initialSlideIndex, open, showTouchControls]);

  React.useEffect(() => {
    setCurrentIndex((previousIndex) => clampIndex(previousIndex, items.length));
  }, [items.length]);

  React.useEffect(() => {
    zoomRef.current = zoom;
  }, [zoom]);

  React.useEffect(() => {
    setZoom({ scale: 1, panX: 0, panY: 0 });
    setZoomAnimating(false);
    setIsPanning(false);
  }, [currentIndex, open]);

  const activeMediaKey = React.useMemo(() => {
    const item = items[currentIndex];
    if (!item) {
      return "";
    }

    const resolvedUrl = signedUrls[item.id] ?? displayUrls[item.id] ?? item.previewUrl;
    return `${item.id}:${resolvedUrl}`;
  }, [currentIndex, displayUrls, items, signedUrls]);

  React.useEffect(() => {
    if (!open) {
      return;
    }

    if (!smoothTransitions) {
      setMediaVisible(true);
      return;
    }

    setMediaVisible(false);

    if (mediaRevealFrameRef.current !== null) {
      window.cancelAnimationFrame(mediaRevealFrameRef.current);
    }

    mediaRevealFrameRef.current = window.requestAnimationFrame(() => {
      setMediaVisible(true);
      mediaRevealFrameRef.current = null;
    });

    return () => {
      if (mediaRevealFrameRef.current !== null) {
        window.cancelAnimationFrame(mediaRevealFrameRef.current);
        mediaRevealFrameRef.current = null;
      }
    };
  }, [activeMediaKey, open, smoothTransitions]);

  const preloadImageUrl = React.useCallback(async (url: string): Promise<boolean> => {
    if (warmedImageUrlsRef.current.has(url)) {
      return true;
    }

    const existingTask = inFlightImagePreloadsRef.current.get(url);
    if (existingTask) {
      return await existingTask;
    }

    const loadTask = new Promise<boolean>((resolve) => {
      const image = new Image();
      image.decoding = "async";

      image.onload = () => {
        warmedImageUrlsRef.current.add(url);
        inFlightImagePreloadsRef.current.delete(url);
        resolve(true);
      };

      image.onerror = () => {
        inFlightImagePreloadsRef.current.delete(url);
        resolve(false);
      };

      image.src = url;
    });

    inFlightImagePreloadsRef.current.set(url, loadTask);
    return await loadTask;
  }, []);

  const ensureSlideHasOriginal = React.useCallback(
    async (targetIndex: number): Promise<void> => {
      const item = items[targetIndex];
      if (!item) {
        return;
      }

      const signedUrlAlreadyLoaded = Boolean(signedUrlsRef.current[item.id]);
      const displayUrlAlreadyLoaded =
        item.kind === "video" ||
        displayUrlsRef.current[item.id] === signedUrlsRef.current[item.id];
      const isFullyLoaded =
        signedUrlAlreadyLoaded &&
        displayUrlAlreadyLoaded;

      if (isFullyLoaded) {
        return;
      }

      const existingInFlight = inFlightLoadsRef.current.get(item.id);
      if (existingInFlight) {
        await existingInFlight;
        return;
      }

      const loadTask = (async () => {
        try {
          let signedUrl = signedUrlsRef.current[item.id];

          if (!signedUrl) {
            signedUrl = await getSignedMediaUrl(item.id);
            setSignedUrls((previous) =>
              previous[item.id] ? previous : { ...previous, [item.id]: signedUrl },
            );
          }

          if (item.kind === "image" && displayUrlsRef.current[item.id] !== signedUrl) {
            const didPreload = await preloadImageUrl(signedUrl);
            if (!didPreload) {
              return;
            }

            setDisplayUrls((previous) =>
              previous[item.id] === signedUrl
                ? previous
                : { ...previous, [item.id]: signedUrl },
            );
          }

          if (item.kind !== "image" && displayUrlsRef.current[item.id] !== signedUrl) {
            setDisplayUrls((previous) =>
              previous[item.id] === signedUrl
                ? previous
                : { ...previous, [item.id]: signedUrl },
            );
          }
        } catch (error) {
          console.error("Failed to load media URL", error);
        } finally {
          inFlightLoadsRef.current.delete(item.id);
        }
      })();

      inFlightLoadsRef.current.set(item.id, loadTask);
      await loadTask;
    },
    [getSignedMediaUrl, items, preloadImageUrl],
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

      try {
        const nextUrl = await getDownloadUrl(fileId);
        setDownloadUrls((previous) =>
          previous[fileId] ? previous : { ...previous, [fileId]: nextUrl },
        );
        return nextUrl;
      } catch (error) {
        console.error("Failed to load media download URL", error);
        return null;
      }
    },
    [getDownloadUrl],
  );

  React.useEffect(() => {
    if (!open) {
      return;
    }

    for (const offset of LIGHTBOX_PREFETCH_OFFSETS) {
      void ensureSlideHasOriginal(currentIndex + offset);
    }
  }, [currentIndex, ensureSlideHasOriginal, open]);

  React.useEffect(() => {
    if (!open) {
      return;
    }

    for (const offset of [-1, 1]) {
      const targetIndex = currentIndex + offset;
      const item = items[targetIndex];

      if (!item || item.kind !== "image") {
        continue;
      }

      void ensureSlideHasOriginal(targetIndex);
    }
  }, [currentIndex, ensureSlideHasOriginal, items, open]);

  const clearSlideTransition = React.useCallback(() => {
    if (slideTransitionTimeoutRef.current !== null) {
      window.clearTimeout(slideTransitionTimeoutRef.current);
      slideTransitionTimeoutRef.current = null;
    }

    if (slideTransitionFrameRef.current !== null) {
      window.cancelAnimationFrame(slideTransitionFrameRef.current);
      slideTransitionFrameRef.current = null;
    }
  }, []);

  const startSlideTransition = React.useCallback(
    (fromIndex: number, toIndex: number) => {
      clearSlideTransition();

      if (!smoothTransitions || fromIndex === toIndex) {
        setSlideTransition(null);
        return;
      }

      const direction: 1 | -1 = toIndex > fromIndex ? 1 : -1;
      setSlideTransition({ fromIndex, toIndex, direction, phase: "enter" });

      slideTransitionFrameRef.current = window.requestAnimationFrame(() => {
        setSlideTransition((previous) => {
          if (!previous || previous.fromIndex !== fromIndex || previous.toIndex !== toIndex) {
            return previous;
          }

          return { ...previous, phase: "active" };
        });
        slideTransitionFrameRef.current = null;
      });

      slideTransitionTimeoutRef.current = window.setTimeout(() => {
        setSlideTransition(null);
        slideTransitionTimeoutRef.current = null;
      }, LIGHTBOX_ANIMATION_MS + 40);
    },
    [clearSlideTransition, smoothTransitions],
  );

  const goToIndex = React.useCallback(
    (targetIndex: number, revealControls: boolean = false) => {
      const nextIndex = clampIndex(targetIndex, items.length);
      if (nextIndex === currentIndex) {
        return;
      }

      startSlideTransition(currentIndex, nextIndex);
      setCurrentIndex(nextIndex);

      if (revealControls) {
        showTouchControls();
      }
    },
    [currentIndex, items.length, showTouchControls, startSlideTransition],
  );

  const handlePrev = React.useCallback(() => {
    goToIndex(currentIndex - 1, true);
  }, [currentIndex, goToIndex]);

  const handleNext = React.useCallback(() => {
    goToIndex(currentIndex + 1, true);
  }, [currentIndex, goToIndex]);

  React.useEffect(() => {
    if (!open) {
      return;
    }

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        onClose();
        return;
      }

      if (event.key === "ArrowLeft") {
        event.preventDefault();
        goToIndex(currentIndex - 1, true);
        return;
      }

      if (event.key === "ArrowRight") {
        event.preventDefault();
        goToIndex(currentIndex + 1, true);
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [currentIndex, goToIndex, onClose, open]);

  const currentItem = items[currentIndex] ?? null;
  const currentSignedUrl = currentItem ? signedUrls[currentItem.id] ?? null : null;
  const currentDisplayUrl = currentItem
    ? displayUrls[currentItem.id] || currentItem.previewUrl || currentSignedUrl || TRANSPARENT_PLACEHOLDER
    : TRANSPARENT_PLACEHOLDER;
  const currentDownloadUrl = currentItem
    ? downloadUrls[currentItem.id] ?? currentSignedUrl
    : null;
  const canDownloadCurrentItem = Boolean(currentItem) && Boolean(getDownloadUrl || currentSignedUrl);
  const currentShareUrl = React.useMemo(
    () => buildShareUrl(currentDownloadUrl),
    [currentDownloadUrl],
  );

  const handleDownload = React.useCallback(async () => {
    if (!currentItem) {
      return;
    }

    showTouchControls();
    await ensureSlideHasOriginal(currentIndex);

    const downloadUrl =
      (await ensureDownloadUrl(currentItem.id)) ?? signedUrlsRef.current[currentItem.id];

    if (!downloadUrl) {
      return;
    }

    triggerDownload(downloadUrl, currentItem.name);
  }, [currentIndex, currentItem, ensureDownloadUrl, ensureSlideHasOriginal, showTouchControls]);

  const handleShare = React.useCallback(async () => {
    if (!currentItem) {
      return;
    }

    showTouchControls();
    await ensureSlideHasOriginal(currentIndex);

    const candidateUrl =
      downloadUrlsRef.current[currentItem.id] ?? signedUrlsRef.current[currentItem.id] ?? null;
    const shareUrl = buildShareUrl(candidateUrl);

    if (!shareUrl) {
      return;
    }

    const outcome = await shareLinkAction({
      title: currentItem.name,
      text: t("message", { ns: "share", name: currentItem.name }),
      url: shareUrl,
    });

    if (outcome.kind === "shared") {
      setFeedbackMessage(t("toasts.shared", { ns: "share" }));
      setFeedbackOpen(true);
      return;
    }

    if (outcome.kind === "copied") {
      setFeedbackMessage(t("toasts.copied", { ns: "share" }));
      setFeedbackOpen(true);
      return;
    }

    if (outcome.kind === "error") {
      setFeedbackMessage(t("errors.copyLink", { ns: "share" }));
      setFeedbackOpen(true);
    }
  }, [currentIndex, currentItem, ensureSlideHasOriginal, showTouchControls, t]);

  const controlsVisible = isTouchDevice ? touchControlsVisible : desktopControlsVisible;
  const canGoPrev = currentIndex > 0;
  const canGoNext = currentIndex < items.length - 1;
  const transitionStyle = smoothTransitions
    ? `opacity ${LIGHTBOX_ANIMATION_MS}ms ease, transform ${LIGHTBOX_ANIMATION_MS}ms ease`
    : "none";
  const currentMeta = buildMetaLabel(currentItem, currentIndex, items.length);
  const currentBackgroundUrl =
    currentItem?.previewUrl || currentDisplayUrl || currentSignedUrl || TRANSPARENT_PLACEHOLDER;

  React.useEffect(() => {
    if (currentBackgroundUrl === displayedBgRef.current) {
      return;
    }

    const prevUrl = displayedBgRef.current;
    displayedBgRef.current = currentBackgroundUrl;

    if (!smoothTransitions) {
      setBgLayers({ bottom: currentBackgroundUrl, top: currentBackgroundUrl });
      setBgTopOpacity(1);
      return;
    }

    setBgLayers({ bottom: prevUrl, top: currentBackgroundUrl });
    setBgTopOpacity(0);

    if (bgCrossfadeFrameRef.current !== null) {
      window.cancelAnimationFrame(bgCrossfadeFrameRef.current);
    }

    bgCrossfadeFrameRef.current = window.requestAnimationFrame(() => {
      setBgTopOpacity(1);
      bgCrossfadeFrameRef.current = null;
    });
  }, [currentBackgroundUrl, smoothTransitions]);

  const isVideoLoading = currentItem?.kind === "video" && !currentSignedUrl;
  const isImageLoading =
    currentItem?.kind === "image" &&
    (!currentSignedUrl || displayUrls[currentItem.id] !== currentSignedUrl);
  const showThumbnailStrip =
    !isTouchDevice &&
    currentItem?.kind === "image" &&
    items.length > 1 &&
    isThumbnailStripHovered;
  const canZoomCurrentItem = Boolean(open && currentItem?.kind === "image");

  const clampZoomState = React.useCallback(
    (nextZoom: { scale: number; panX: number; panY: number }, item: MediaItem | null) => {
      const nextScale = clampValue(nextZoom.scale, 1, MAX_IMAGE_ZOOM);
      if (nextScale <= 1 || !item || item.kind !== "image") {
        return { scale: 1, panX: 0, panY: 0 };
      }

      const viewport = mediaViewportRef.current?.getBoundingClientRect();
      if (!viewport || viewport.width <= 0 || viewport.height <= 0) {
        return { scale: nextScale, panX: nextZoom.panX, panY: nextZoom.panY };
      }

      let mediaWidth = viewport.width;
      let mediaHeight = viewport.height;

      if (item.width && item.height && item.width > 0 && item.height > 0) {
        const mediaRatio = item.width / item.height;
        const viewportRatio = viewport.width / viewport.height;

        if (mediaRatio > viewportRatio) {
          mediaWidth = viewport.width;
          mediaHeight = viewport.width / mediaRatio;
        } else {
          mediaHeight = viewport.height;
          mediaWidth = viewport.height * mediaRatio;
        }
      }

      const maxPanX = Math.max(0, ((mediaWidth * nextScale) - mediaWidth) / 2);
      const maxPanY = Math.max(0, ((mediaHeight * nextScale) - mediaHeight) / 2);

      return {
        scale: nextScale,
        panX: clampValue(nextZoom.panX, -maxPanX, maxPanX),
        panY: clampValue(nextZoom.panY, -maxPanY, maxPanY),
      };
    },
    [],
  );

  const setClampedZoom = React.useCallback(
    (
      value:
        | { scale: number; panX: number; panY: number }
        | ((previous: { scale: number; panX: number; panY: number }) => {
            scale: number;
            panX: number;
            panY: number;
          }),
    ) => {
      setZoom((previous) => {
        const nextValue = typeof value === "function" ? value(previous) : value;
        const clamped = clampZoomState(nextValue, currentItem);
        zoomRef.current = clamped;
        return clamped;
      });
    },
    [clampZoomState, currentItem],
  );

  const getBackgroundUrl = React.useCallback(
    (item: MediaItem | null) => {
      if (!item) {
        return TRANSPARENT_PLACEHOLDER;
      }

      return item.previewUrl || displayUrls[item.id] || signedUrls[item.id] || TRANSPARENT_PLACEHOLDER;
    },
    [displayUrls, signedUrls],
  );

  const transitionBackground = React.useMemo(() => {
    if (!slideTransition) {
      return null;
    }

    return {
      bottom: getBackgroundUrl(items[slideTransition.fromIndex] ?? null),
      top: getBackgroundUrl(items[slideTransition.toIndex] ?? null),
      topOpacity: slideTransition.phase === "active" ? 1 : 0,
    };
  }, [getBackgroundUrl, items, slideTransition]);

  const visibleThumbnailItems = React.useMemo(() => {
    if (!showThumbnailStrip) {
      return [] as Array<{ item: MediaItem; index: number }>;
    }

    const startIndex = Math.max(0, currentIndex - THUMBNAIL_WINDOW_RADIUS);
    const endIndex = Math.min(items.length, currentIndex + THUMBNAIL_WINDOW_RADIUS + 1);

    return items.slice(startIndex, endIndex).map((item, offset) => ({
      item,
      index: startIndex + offset,
    }));
  }, [currentIndex, items, showThumbnailStrip]);

  const getCursorRelativeToCenter = React.useCallback((clientX: number, clientY: number) => {
    const viewport = mediaViewportRef.current;
    if (!viewport) {
      return { x: 0, y: 0 };
    }

    const rect = viewport.getBoundingClientRect();
    return {
      x: clientX - rect.left - rect.width / 2,
      y: clientY - rect.top - rect.height / 2,
    };
  }, []);

  const animateZoomTransition = React.useCallback(() => {
    setZoomAnimating(true);

    if (zoomAnimateTimerRef.current !== null) {
      window.clearTimeout(zoomAnimateTimerRef.current);
    }

    zoomAnimateTimerRef.current = window.setTimeout(() => {
      setZoomAnimating(false);
      zoomAnimateTimerRef.current = null;
    }, ZOOM_ANIMATE_MS + 20);
  }, []);

  const toggleZoomAtPoint = React.useCallback(
    (clientX: number, clientY: number) => {
      if (!canZoomCurrentItem) {
        return;
      }

      const cursor = getCursorRelativeToCenter(clientX, clientY);
      setClampedZoom((prev) => {
        if (prev.scale > 1) {
          return { scale: 1, panX: 0, panY: 0 };
        }

        const nextScale = DEFAULT_IMAGE_ZOOM;
        return {
          scale: nextScale,
          panX: cursor.x - cursor.x * nextScale,
          panY: cursor.y - cursor.y * nextScale,
        };
      });
      animateZoomTransition();
    },
    [animateZoomTransition, canZoomCurrentItem, getCursorRelativeToCenter, setClampedZoom],
  );

  const handleMediaWheel = React.useCallback(
    (event: React.WheelEvent<HTMLDivElement>) => {
      if (!canZoomCurrentItem) {
        return;
      }

      event.preventDefault();
      event.stopPropagation();

      const cursor = getCursorRelativeToCenter(event.clientX, event.clientY);
      const factor = event.deltaY < 0 ? ZOOM_WHEEL_FACTOR : 1 / ZOOM_WHEEL_FACTOR;

      setClampedZoom((prev) => {
        const nextScale = Math.min(MAX_IMAGE_ZOOM, Math.max(1, prev.scale * factor));

        if (nextScale <= 1) {
          return { scale: 1, panX: 0, panY: 0 };
        }

        const ratio = nextScale / prev.scale;
        return {
          scale: nextScale,
          panX: cursor.x - (cursor.x - prev.panX) * ratio,
          panY: cursor.y - (cursor.y - prev.panY) * ratio,
        };
      });
    },
    [canZoomCurrentItem, getCursorRelativeToCenter, setClampedZoom],
  );

  const handleMediaDoubleClick = React.useCallback(
    (event: React.MouseEvent<HTMLDivElement>) => {
      if (shouldIgnoreGestureTarget(event.target)) {
        return;
      }

      if (isPanning) {
        return;
      }

      toggleZoomAtPoint(event.clientX, event.clientY);
    },
    [isPanning, toggleZoomAtPoint],
  );

  const handlePanMouseDown = React.useCallback(
    (event: React.MouseEvent<HTMLDivElement>) => {
      const currentZoom = zoomRef.current;
      if (currentZoom.scale <= 1 || event.button !== 0) {
        return;
      }

      if (shouldIgnoreGestureTarget(event.target)) {
        return;
      }

      const startX = event.clientX;
      const startY = event.clientY;
      const startPanX = currentZoom.panX;
      const startPanY = currentZoom.panY;
      let didMove = false;

      const handleMouseMove = (moveEvent: MouseEvent) => {
        const dx = moveEvent.clientX - startX;
        const dy = moveEvent.clientY - startY;

        if (!didMove && Math.abs(dx) < 4 && Math.abs(dy) < 4) {
          return;
        }

        didMove = true;
        setIsPanning(true);
        setClampedZoom((prev) => ({ ...prev, panX: startPanX + dx, panY: startPanY + dy }));
      };

      const handleMouseUp = () => {
        window.removeEventListener("mousemove", handleMouseMove);
        window.removeEventListener("mouseup", handleMouseUp);

        if (didMove) {
          setTimeout(() => setIsPanning(false), 0);
        }
      };

      window.addEventListener("mousemove", handleMouseMove);
      window.addEventListener("mouseup", handleMouseUp);
    },
    [setClampedZoom],
  );

  const handleViewportDragEvent = React.useCallback((event: React.DragEvent<HTMLDivElement>) => {
    stopEvent(event);
  }, []);

  React.useEffect(() => {
    if (!open) {
      return;
    }

    setClampedZoom((previous) => previous);
  }, [currentItem, open, setClampedZoom]);

  const getSlideUrls = React.useCallback(
    (item: MediaItem | null) => {
      if (!item) {
        return {
          previewUrl: TRANSPARENT_PLACEHOLDER,
          finalUrl: null as string | null,
          displayUrl: TRANSPARENT_PLACEHOLDER,
        };
      }

      const signedUrl = signedUrls[item.id] ?? null;
      const finalReady = Boolean(signedUrl && displayUrls[item.id] === signedUrl);

      return {
        previewUrl: item.previewUrl || TRANSPARENT_PLACEHOLDER,
        finalUrl: finalReady ? signedUrl : null,
        displayUrl:
          displayUrls[item.id] || item.previewUrl || signedUrl || TRANSPARENT_PLACEHOLDER,
      };
    },
    [displayUrls, signedUrls],
  );

  const renderImageSlide = React.useCallback(
    (item: MediaItem, interactive: boolean) => {
      const urls = getSlideUrls(item);
      const activeZoom = interactive ? zoom : { scale: 1, panX: 0, panY: 0 };
      const zoomTransform = `translate(${activeZoom.panX}px, ${activeZoom.panY}px) scale(${activeZoom.scale})`;
      const shouldAnimateZoom = interactive && zoomAnimating;

      const transitionParts: string[] = [];
      if (shouldAnimateZoom) {
        transitionParts.push(`transform ${ZOOM_ANIMATE_MS}ms ease-out`);
      }
      if (smoothTransitions) {
        transitionParts.push(`opacity ${LIGHTBOX_ANIMATION_MS}ms ease`);
      }
      const imageTransition = transitionParts.length > 0 ? transitionParts.join(", ") : "none";

      return (
        <Box
          sx={{
            position: "relative",
            width: "100%",
            height: "100%",
            overflow: "hidden",
          }}
        >
          <Box
            component="img"
            src={urls.previewUrl}
            alt={item.name}
            draggable={false}
            sx={{
              position: "absolute",
              inset: 0,
              display: "block",
              width: "100%",
              height: "100%",
              maxWidth: "100%",
              maxHeight: "100%",
              objectFit: "contain",
              transform: zoomTransform,
              transformOrigin: "center center",
              transition: imageTransition,
              willChange: interactive ? "transform, opacity" : "opacity",
              backfaceVisibility: "hidden",
            }}
          />
          {urls.finalUrl ? (
            <Box
              component="img"
              src={urls.finalUrl}
              alt={item.name}
              draggable={false}
              sx={{
                position: "absolute",
                inset: 0,
                display: "block",
                width: "100%",
                height: "100%",
                maxWidth: "100%",
                maxHeight: "100%",
                objectFit: "contain",
                transform: zoomTransform,
                transformOrigin: "center center",
                opacity: mediaVisible ? 1 : 0,
                transition: imageTransition,
                willChange: interactive ? "transform, opacity" : "opacity",
                backfaceVisibility: "hidden",
              }}
            />
          ) : null}
        </Box>
      );
    },
    [getSlideUrls, mediaVisible, smoothTransitions, zoom, zoomAnimating],
  );

  const renderMediaSlide = React.useCallback(
    (item: MediaItem, interactive: boolean) => {
      if (item.kind === "image") {
        return renderImageSlide(item, interactive);
      }

      const signedUrl = signedUrls[item.id] ?? null;
      if (signedUrl) {
        return (
          <Box
            component="video"
            src={signedUrl}
            poster={item.previewUrl || undefined}
            controls
            autoPlay
            playsInline
            draggable={false}
            sx={{
              display: "block",
              width: "100%",
              height: "100%",
              maxWidth: "100%",
              maxHeight: "100%",
              objectFit: "contain",
              opacity: mediaVisible ? 1 : 0,
              transition: transitionStyle,
            }}
          />
        );
      }

      return (
        <Box
          component="img"
          src={item.previewUrl || TRANSPARENT_PLACEHOLDER}
          alt={item.name}
          draggable={false}
          sx={{
            display: "block",
            width: "100%",
            height: "100%",
            maxWidth: "100%",
            maxHeight: "100%",
            objectFit: "contain",
            opacity: mediaVisible ? 0.82 : 0,
            transition: transitionStyle,
          }}
        />
      );
    },
    [mediaVisible, renderImageSlide, signedUrls, transitionStyle],
  );

  React.useEffect(() => {
    if (!showThumbnailStrip) {
      return;
    }

    const item = items[currentIndex];
    if (!item) {
      return;
    }

    const strip = thumbnailStripRef.current;
    const button = thumbnailButtonRefs.current[item.id];

    if (!strip || !button) {
      return;
    }

    const nextScrollLeft =
      button.offsetLeft - strip.clientWidth / 2 + button.clientWidth / 2;

    strip.scrollTo({
      left: Math.max(0, nextScrollLeft),
      behavior: smoothTransitions ? "smooth" : "auto",
    });
  }, [currentIndex, items, showThumbnailStrip, smoothTransitions]);

  const handleTouchStart = React.useCallback(
    (event: React.TouchEvent) => {
      if (!isTouchDevice) {
        return;
      }

      showTouchControls();

      if (event.touches.length === 2 && canZoomCurrentItem) {
        event.preventDefault();
        const [firstTouch, secondTouch] = [event.touches[0], event.touches[1]];
        pinchGestureRef.current = {
          startDistance: getTouchDistance(firstTouch, secondTouch),
          startScale: zoomRef.current.scale,
          startPanX: zoomRef.current.panX,
          startPanY: zoomRef.current.panY,
        };
        touchGestureRef.current = null;
        return;
      }

      if (event.touches.length !== 1 || shouldIgnoreGestureTarget(event.target)) {
        touchGestureRef.current = null;
        return;
      }

      const touch = event.touches[0];
      touchGestureRef.current = {
        startX: touch.clientX,
        startY: touch.clientY,
        lastX: touch.clientX,
        lastY: touch.clientY,
      };
    },
    [canZoomCurrentItem, isTouchDevice, showTouchControls],
  );

  const handleTouchMove = React.useCallback((event: React.TouchEvent) => {
    if (event.touches.length === 2 && pinchGestureRef.current && canZoomCurrentItem) {
      event.preventDefault();
      const [firstTouch, secondTouch] = [event.touches[0], event.touches[1]];
      const distance = getTouchDistance(firstTouch, secondTouch);
      const pinch = pinchGestureRef.current;
      const midpointX = (firstTouch.clientX + secondTouch.clientX) / 2;
      const midpointY = (firstTouch.clientY + secondTouch.clientY) / 2;
      const cursor = getCursorRelativeToCenter(midpointX, midpointY);
      const startScale = Math.max(1, pinch.startScale);
      const nextScale = pinch.startDistance > 0
        ? pinch.startScale * (distance / pinch.startDistance)
        : pinch.startScale;
      const ratio = nextScale / startScale;

      setClampedZoom({
        scale: nextScale,
        panX: cursor.x - (cursor.x - pinch.startPanX) * ratio,
        panY: cursor.y - (cursor.y - pinch.startPanY) * ratio,
      });
      return;
    }

    if (!touchGestureRef.current || event.touches.length !== 1) {
      return;
    }

    event.preventDefault();

    const touch = event.touches[0];
    const prev = touchGestureRef.current;

    if (zoomRef.current.scale > 1) {
      const dx = touch.clientX - prev.lastX;
      const dy = touch.clientY - prev.lastY;
      setClampedZoom((z) => ({ ...z, panX: z.panX + dx, panY: z.panY + dy }));
    }

    touchGestureRef.current = {
      ...prev,
      lastX: touch.clientX,
      lastY: touch.clientY,
    };
  }, [canZoomCurrentItem, getCursorRelativeToCenter, setClampedZoom]);

  const handleTouchEnd = React.useCallback((event: React.TouchEvent) => {
    if (pinchGestureRef.current) {
      if (event.touches.length === 1) {
        const touch = event.touches[0];
        touchGestureRef.current = {
          startX: touch.clientX,
          startY: touch.clientY,
          lastX: touch.clientX,
          lastY: touch.clientY,
        };
      }

      if (event.touches.length < 2) {
        pinchGestureRef.current = null;
      }

      if (event.touches.length > 0) {
        return;
      }
    }

    const gesture = touchGestureRef.current;
    touchGestureRef.current = null;

    if (!gesture) {
      return;
    }

    const deltaX = gesture.lastX - gesture.startX;
    const deltaY = gesture.lastY - gesture.startY;
    const absX = Math.abs(deltaX);
    const absY = Math.abs(deltaY);

    if (canZoomCurrentItem && absX < 12 && absY < 12) {
      const now = window.performance.now();
      const previousTap = lastTapRef.current;

      if (
        previousTap &&
        now - previousTap.time <= DOUBLE_TAP_DELAY_MS &&
        Math.abs(previousTap.x - gesture.lastX) < 20 &&
        Math.abs(previousTap.y - gesture.lastY) < 20
      ) {
        toggleZoomAtPoint(gesture.lastX, gesture.lastY);
        lastTapRef.current = null;
        return;
      }

      lastTapRef.current = {
        time: now,
        x: gesture.lastX,
        y: gesture.lastY,
      };
    } else {
      lastTapRef.current = null;
    }

    if (zoomRef.current.scale > 1) {
      return;
    }

    if (absY >= VERTICAL_CLOSE_SWIPE_PX && absY > absX * 1.15) {
      onClose();
      return;
    }

    if (absX >= HORIZONTAL_SWIPE_PX && absX > absY * 1.1) {
      if (deltaX < 0) {
        goToIndex(currentIndex + 1);
      } else {
        goToIndex(currentIndex - 1);
      }
    }
  }, [canZoomCurrentItem, currentIndex, goToIndex, onClose, toggleZoomAtPoint]);

  if (items.length === 0 || !currentItem) {
    return null;
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      fullScreen
      keepMounted
      TransitionComponent={Grow}
      TransitionProps={{
        timeout: smoothTransitions ? LIGHTBOX_ANIMATION_MS : 0,
        style: { transformOrigin: "center center" },
      }}
      transitionDuration={smoothTransitions ? LIGHTBOX_ANIMATION_MS : 0}
      PaperProps={{
        sx: {
          bgcolor: "common.black",
          backgroundImage: "none",
          color: "common.white",
          overflow: "hidden",
          userSelect: "none",
          WebkitUserSelect: "none",
        },
      }}
    >
      <Box
        ref={mediaViewportRef}
        sx={{
          position: "relative",
          width: "100%",
          height: "100%",
          overflow: "hidden",
          bgcolor: "rgba(5, 7, 10, 0.98)",
          touchAction: "none",
          cursor: isPanning
            ? "grabbing"
            : zoom.scale > 1
              ? "grab"
              : controlsVisible
                ? "default"
                : "none",
          userSelect: "none",
          WebkitUserSelect: "none",
        }}
        onDragStartCapture={(event) => event.preventDefault()}
        onDragEnterCapture={handleViewportDragEvent}
        onDragOverCapture={handleViewportDragEvent}
        onDragLeaveCapture={handleViewportDragEvent}
        onDropCapture={handleViewportDragEvent}
        onTouchStart={handleTouchStart}
        onTouchMove={handleTouchMove}
        onTouchEnd={handleTouchEnd}
        onTouchCancel={() => {
          pinchGestureRef.current = null;
          touchGestureRef.current = null;
        }}
        onWheel={handleMediaWheel}
        onDoubleClick={handleMediaDoubleClick}
        onMouseDown={handlePanMouseDown}
        onMouseLeave={() => {
          setIsPanning(false);
          setIsThumbnailStripHovered(false);
        }}
      >
        <Box
          aria-hidden="true"
          sx={{
            position: "absolute",
            inset: 0,
            opacity: 0.48,
            pointerEvents: "none",
            userSelect: "none",
            overflow: "hidden",
          }}
        >
          <Box
            component="img"
            src={transitionBackground?.bottom ?? bgLayers.bottom}
            alt=""
            sx={{
              position: "absolute",
              inset: 0,
              width: "100%",
              height: "100%",
              objectFit: "cover",
              filter: "blur(28px)",
              transform: "scale(1.15)",
              willChange: "opacity",
            }}
          />
          <Box
            component="img"
            src={transitionBackground?.top ?? bgLayers.top}
            alt=""
            sx={{
              position: "absolute",
              inset: 0,
              width: "100%",
              height: "100%",
              objectFit: "cover",
              filter: "blur(28px)",
              transform: "scale(1.15)",
              opacity: transitionBackground?.topOpacity ?? bgTopOpacity,
              transition: smoothTransitions
                ? `opacity ${BACKGROUND_CROSSFADE_MS}ms ease`
                : "none",
              willChange: "opacity",
            }}
          />
        </Box>

        <Box
          sx={{
            position: "absolute",
            inset: 0,
            background:
              "linear-gradient(180deg, rgba(0, 0, 0, 0.22) 0%, rgba(0, 0, 0, 0.04) 24%, rgba(0, 0, 0, 0.08) 100%)",
            pointerEvents: "none",
          }}
        />

        <Box
          sx={{
            position: "absolute",
            top: 0,
            left: 0,
            right: 0,
            zIndex: 3,
            display: "flex",
            alignItems: "center",
            gap: { xs: 0.75, sm: 2 },
            px: { xs: 1.5, sm: 2 },
            py: { xs: "calc(env(safe-area-inset-top, 0px) + 8px)", sm: "calc(env(safe-area-inset-top, 0px) + 12px)" },
            minHeight: { xs: "calc(env(safe-area-inset-top, 0px) + 44px)", sm: "calc(env(safe-area-inset-top, 0px) + 56px)" },
            opacity: controlsVisible ? 1 : 0,
            pointerEvents: controlsVisible ? "auto" : "none",
            transition: transitionStyle,
          }}
        >
          <Box sx={{ minWidth: 0, flex: 1 }}>
            <Typography
              variant="body2"
              noWrap
              sx={{
                fontWeight: 600,
                letterSpacing: 0.1,
                opacity: 0.9,
              }}
            >
              {currentMeta}
            </Typography>
          </Box>

          {currentShareUrl ? (
            <Tooltip title={t("actions.share", { ns: "common" })}>
              <IconButton
                onClick={() => {
                  void handleShare();
                }}
                aria-label={t("actions.share", { ns: "common" })}
                size="small"
                sx={{
                  color: "common.white",
                  bgcolor: "rgba(0, 0, 0, 0.32)",
                  width: { xs: 34, sm: 40 },
                  height: { xs: 34, sm: 40 },
                }}
              >
                <ShareIcon sx={{ fontSize: { xs: 18, sm: 22 } }} />
              </IconButton>
            </Tooltip>
          ) : null}

          {canDownloadCurrentItem ? (
            <Tooltip title={t("actions.download", { ns: "common" })}>
              <IconButton
                onClick={() => {
                  void handleDownload();
                }}
                aria-label={t("actions.download", { ns: "common" })}
                size="small"
                sx={{
                  color: "common.white",
                  bgcolor: "rgba(0, 0, 0, 0.32)",
                  width: { xs: 34, sm: 40 },
                  height: { xs: 34, sm: 40 },
                }}
              >
                <DownloadIcon sx={{ fontSize: { xs: 18, sm: 22 } }} />
              </IconButton>
            </Tooltip>
          ) : null}

          <Tooltip title={t("actions.close", { ns: "common" })}>
            <IconButton
              onClick={onClose}
              aria-label={t("actions.close", { ns: "common" })}
              size="small"
              sx={{
                color: "common.white",
                bgcolor: "rgba(0, 0, 0, 0.32)",
                width: { xs: 34, sm: 40 },
                height: { xs: 34, sm: 40 },
              }}
            >
              <Close sx={{ fontSize: { xs: 18, sm: 22 } }} />
            </IconButton>
          </Tooltip>
        </Box>

        {items.length > 1 ? (
          <>
            <IconButton
              onClick={handlePrev}
              disabled={!canGoPrev}
              aria-label={t("actions.previous", { ns: "common" })}
              sx={{
                position: "absolute",
                left: { xs: 8, sm: 16 },
                top: "50%",
                zIndex: 3,
                transform: "translateY(-50%)",
                color: "common.white",
                bgcolor: "rgba(0, 0, 0, 0.32)",
                opacity: controlsVisible ? 1 : 0,
                pointerEvents: controlsVisible ? "auto" : "none",
                transition: transitionStyle,
                "&.Mui-disabled": {
                  color: "rgba(255, 255, 255, 0.25)",
                },
              }}
            >
              <ChevronLeft />
            </IconButton>

            <IconButton
              onClick={handleNext}
              disabled={!canGoNext}
              aria-label={t("actions.next", { ns: "common" })}
              sx={{
                position: "absolute",
                right: { xs: 8, sm: 16 },
                top: "50%",
                zIndex: 3,
                transform: "translateY(-50%)",
                color: "common.white",
                bgcolor: "rgba(0, 0, 0, 0.32)",
                opacity: controlsVisible ? 1 : 0,
                pointerEvents: controlsVisible ? "auto" : "none",
                transition: transitionStyle,
                "&.Mui-disabled": {
                  color: "rgba(255, 255, 255, 0.25)",
                },
              }}
            >
              <ChevronRight />
            </IconButton>
          </>
        ) : null}

        <Box
          sx={{
            position: "absolute",
            inset: 0,
            zIndex: 1,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            px: 0,
            py: 0,
          }}
        >
          {slideTransition ? (
            <>
              <Box
                sx={{
                  position: "absolute",
                  inset: 0,
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  transform:
                    slideTransition.phase === "active"
                      ? `translateX(${slideTransition.direction > 0 ? "-100%" : "100%"})`
                      : "translateX(0%)",
                  transition: smoothTransitions
                    ? `transform ${LIGHTBOX_ANIMATION_MS}ms ease`
                    : "none",
                }}
              >
                {renderMediaSlide(items[slideTransition.fromIndex] ?? currentItem, false)}
              </Box>
              <Box
                sx={{
                  position: "absolute",
                  inset: 0,
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  transform:
                    slideTransition.phase === "active"
                      ? "translateX(0%)"
                      : `translateX(${slideTransition.direction > 0 ? "100%" : "-100%"})`,
                  transition: smoothTransitions
                    ? `transform ${LIGHTBOX_ANIMATION_MS}ms ease`
                    : "none",
                }}
              >
                {renderMediaSlide(items[slideTransition.toIndex] ?? currentItem, true)}
              </Box>
            </>
          ) : (
            renderMediaSlide(currentItem, true)
          )}

          {(isImageLoading || isVideoLoading) ? (
            <Box
              sx={{
                position: "absolute",
                inset: 0,
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                pointerEvents: "none",
              }}
            >
              <CircularProgress size={32} sx={{ color: "common.white" }} />
            </Box>
          ) : null}
        </Box>

        {items.length > 1 ? (
          <>
            <Box
              sx={{
                position: "absolute",
                left: 0,
                right: 0,
                bottom: 0,
                zIndex: 2,
                display: { xs: "none", md: currentItem?.kind === "image" ? "block" : "none" },
                height: THUMBNAIL_REVEAL_ZONE_PX,
              }}
              onMouseEnter={() => {
                setIsThumbnailStripHovered(true);
              }}
            />

            <Box
              sx={{
                position: "absolute",
                left: 0,
                right: 0,
                bottom: 0,
                zIndex: 3,
                display: { xs: "none", md: currentItem?.kind === "image" ? "flex" : "none" },
                justifyContent: "center",
                px: 2,
                pb: "calc(env(safe-area-inset-bottom, 0px) + 12px)",
                pt: 6,
                opacity: showThumbnailStrip ? 1 : 0,
                pointerEvents: showThumbnailStrip ? "auto" : "none",
                transition: transitionStyle,
              }}
              onMouseEnter={() => {
                setIsThumbnailStripHovered(true);
              }}
              onMouseLeave={() => {
                setIsThumbnailStripHovered(false);
              }}
            >
              <Box
                ref={thumbnailStripRef}
                sx={{
                  display: "flex",
                  gap: 1,
                  overflowX: "auto",
                  justifyContent: "flex-start",
                  maxWidth: "min(860px, calc(100vw - 48px))",
                  px: 1,
                  py: 1,
                  borderRadius: 2.5,
                  bgcolor: "rgba(12, 12, 12, 0.54)",
                  backdropFilter: "blur(10px)",
                  scrollbarWidth: "none",
                  "&::-webkit-scrollbar": {
                    display: "none",
                  },
                }}
              >
                {visibleThumbnailItems.map(({ item, index }) => {
                  const thumbnailUrl =
                    item.previewUrl || displayUrls[item.id] || signedUrls[item.id] || TRANSPARENT_PLACEHOLDER;
                  const isSelected = index === currentIndex;

                  return (
                    <Box
                      key={item.id}
                      component="button"
                      type="button"
                      ref={(element: HTMLButtonElement | null) => {
                        thumbnailButtonRefs.current[item.id] = element;
                      }}
                      onClick={() => goToIndex(index, true)}
                      sx={{
                        width: 88,
                        height: 56,
                        p: 0,
                        border: "none",
                        borderRadius: 0.75,
                        overflow: "hidden",
                        cursor: "pointer",
                        position: "relative",
                        flex: "0 0 auto",
                        transform: isSelected ? "translateY(-2px) scale(1.02)" : "none",
                        boxShadow: isSelected
                          ? "0 10px 28px rgba(0, 0, 0, 0.45), 0 0 0 1px rgba(255, 255, 255, 0.18) inset"
                          : "0 0 0 1px rgba(255, 255, 255, 0.08) inset",
                        backgroundColor: "rgba(255, 255, 255, 0.06)",
                        transition: smoothTransitions
                          ? "transform 200ms ease, box-shadow 200ms ease"
                          : "none",
                      }}
                    >
                      <Box
                        component="img"
                        src={thumbnailUrl}
                        alt={item.name}
                        draggable={false}
                        sx={{
                          display: "block",
                          width: "100%",
                          height: "100%",
                          objectFit: "cover",
                        }}
                      />
                    </Box>
                  );
                })}
              </Box>
            </Box>
          </>
        ) : null}

        <Snackbar
          open={feedbackOpen}
          autoHideDuration={FEEDBACK_AUTOHIDE_MS}
          onClose={() => setFeedbackOpen(false)}
          message={feedbackMessage}
        />
      </Box>
    </Dialog>
  );
};
