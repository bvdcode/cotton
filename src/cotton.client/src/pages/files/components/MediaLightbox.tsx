import React from "react";
import {
  Box,
  CircularProgress,
  Dialog,
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

interface TouchGestureState {
  startX: number;
  startY: number;
  lastX: number;
  lastY: number;
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
  const mediaRevealFrameRef = React.useRef<number | null>(null);
  const thumbnailStripRef = React.useRef<HTMLDivElement | null>(null);
  const thumbnailButtonRefs = React.useRef<Record<string, HTMLButtonElement | null>>({});
  const [mediaVisible, setMediaVisible] = React.useState(!smoothTransitions);

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
    };
  }, []);

  React.useEffect(() => {
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
      const downloadUrlAlreadyLoaded = Boolean(downloadUrlsRef.current[item.id]);
      const needsDownloadUrl = Boolean(getDownloadUrl);
      const isFullyLoaded =
        signedUrlAlreadyLoaded &&
        displayUrlAlreadyLoaded &&
        (!needsDownloadUrl || downloadUrlAlreadyLoaded);

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

          if (displayUrlsRef.current[item.id] !== signedUrl) {
            setDisplayUrls((previous) =>
              previous[item.id] === signedUrl
                ? previous
                : { ...previous, [item.id]: signedUrl },
            );
          }

          if (getDownloadUrl && !downloadUrlsRef.current[item.id]) {
            try {
              const downloadUrl = await getDownloadUrl(item.id);
              setDownloadUrls((previous) =>
                previous[item.id]
                  ? previous
                  : { ...previous, [item.id]: downloadUrl },
              );
            } catch (error) {
              console.error("Failed to load media download URL", error);
            }
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
    [getDownloadUrl, getSignedMediaUrl, items],
  );

  React.useEffect(() => {
    if (!open) {
      return;
    }

    for (const offset of LIGHTBOX_PREFETCH_OFFSETS) {
      void ensureSlideHasOriginal(currentIndex + offset);
    }
  }, [currentIndex, ensureSlideHasOriginal, open]);

  const goToIndex = React.useCallback(
    (targetIndex: number, revealControls: boolean = false) => {
      setCurrentIndex(clampIndex(targetIndex, items.length));

      if (revealControls) {
        showTouchControls();
      }
    },
    [items.length, showTouchControls],
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
        setCurrentIndex((previousIndex) => clampIndex(previousIndex - 1, items.length));
        return;
      }

      if (event.key === "ArrowRight") {
        event.preventDefault();
        setCurrentIndex((previousIndex) => clampIndex(previousIndex + 1, items.length));
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [items.length, onClose, open]);

  const currentItem = items[currentIndex] ?? null;
  const currentSignedUrl = currentItem ? signedUrls[currentItem.id] ?? null : null;
  const currentDisplayUrl = currentItem
    ? currentSignedUrl || displayUrls[currentItem.id] || currentItem.previewUrl || TRANSPARENT_PLACEHOLDER
    : TRANSPARENT_PLACEHOLDER;
  const currentDownloadUrl = currentItem
    ? downloadUrls[currentItem.id] ?? currentSignedUrl
    : null;
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
      downloadUrlsRef.current[currentItem.id] ?? signedUrlsRef.current[currentItem.id];

    if (!downloadUrl) {
      return;
    }

    triggerDownload(downloadUrl, currentItem.name);
  }, [currentIndex, currentItem, ensureSlideHasOriginal, showTouchControls]);

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
  const isVideoLoading = currentItem?.kind === "video" && !currentSignedUrl;
  const isImageLoading = currentItem?.kind === "image" && !currentSignedUrl;
  const showThumbnailStrip =
    !isTouchDevice &&
    currentItem.kind === "image" &&
    items.length > 1 &&
    isThumbnailStripHovered;

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
    [isTouchDevice, showTouchControls],
  );

  const handleTouchMove = React.useCallback((event: React.TouchEvent) => {
    if (!touchGestureRef.current || event.touches.length !== 1) {
      return;
    }

    const touch = event.touches[0];
    touchGestureRef.current = {
      ...touchGestureRef.current,
      lastX: touch.clientX,
      lastY: touch.clientY,
    };
  }, []);

  const handleTouchEnd = React.useCallback(() => {
    const gesture = touchGestureRef.current;
    touchGestureRef.current = null;

    if (!gesture) {
      return;
    }

    const deltaX = gesture.lastX - gesture.startX;
    const deltaY = gesture.lastY - gesture.startY;
    const absX = Math.abs(deltaX);
    const absY = Math.abs(deltaY);

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
  }, [currentIndex, goToIndex, onClose]);

  if (!open || items.length === 0 || !currentItem) {
    return null;
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      fullScreen
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
        sx={{
          position: "relative",
          width: "100%",
          height: "100%",
          overflow: "hidden",
          bgcolor: "rgba(5, 7, 10, 0.98)",
          cursor: controlsVisible ? "default" : "none",
          userSelect: "none",
          WebkitUserSelect: "none",
        }}
        onDragStartCapture={(event) => event.preventDefault()}
        onTouchStart={handleTouchStart}
        onTouchMove={handleTouchMove}
        onTouchEnd={handleTouchEnd}
        onTouchCancel={() => {
          touchGestureRef.current = null;
        }}
        onMouseLeave={() => {
          setIsThumbnailStripHovered(false);
        }}
      >
        <Box
          component="img"
          src={currentBackgroundUrl}
          alt=""
          aria-hidden="true"
          sx={{
            position: "absolute",
            inset: 0,
            width: "100%",
            height: "100%",
            objectFit: "cover",
            filter: "blur(28px)",
            transform: "scale(1.15)",
            opacity: 0.35,
            pointerEvents: "none",
            userSelect: "none",
          }}
        />

        <Box
          sx={{
            position: "absolute",
            inset: 0,
            background:
              "linear-gradient(180deg, rgba(0, 0, 0, 0.42) 0%, rgba(0, 0, 0, 0.14) 24%, rgba(0, 0, 0, 0.18) 100%)",
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
            gap: 2,
            px: { xs: 1.5, sm: 2 },
            py: "calc(env(safe-area-inset-top, 0px) + 12px)",
            minHeight: "calc(env(safe-area-inset-top, 0px) + 56px)",
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
                sx={{ color: "common.white", bgcolor: "rgba(0, 0, 0, 0.32)" }}
              >
                <ShareIcon />
              </IconButton>
            </Tooltip>
          ) : null}

          {currentDownloadUrl ? (
            <Tooltip title={t("actions.download", { ns: "common" })}>
              <IconButton
                onClick={() => {
                  void handleDownload();
                }}
                aria-label={t("actions.download", { ns: "common" })}
                sx={{ color: "common.white", bgcolor: "rgba(0, 0, 0, 0.32)" }}
              >
                <DownloadIcon />
              </IconButton>
            </Tooltip>
          ) : null}

          <Tooltip title={t("actions.close", { ns: "common" })}>
            <IconButton
              onClick={onClose}
              aria-label={t("actions.close", { ns: "common" })}
              sx={{ color: "common.white", bgcolor: "rgba(0, 0, 0, 0.32)" }}
            >
              <Close />
            </IconButton>
          </Tooltip>
        </Box>

        {items.length > 1 ? (
          <>
            <Tooltip title={t("actions.previous", { ns: "common" })}>
              <Box component="span">
                <IconButton
                  onClick={handlePrev}
                  disabled={!canGoPrev}
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
              </Box>
            </Tooltip>

            <Tooltip title={t("actions.next", { ns: "common" })}>
              <Box component="span">
                <IconButton
                  onClick={handleNext}
                  disabled={!canGoNext}
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
              </Box>
            </Tooltip>
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
          {currentItem.kind === "image" ? (
            <Box
              component="img"
              key={`${currentItem.id}:${currentDisplayUrl}`}
              src={currentDisplayUrl}
              alt={currentItem.name}
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
          ) : currentSignedUrl ? (
            <Box
              component="video"
              key={`${currentItem.id}:${currentSignedUrl}`}
              src={currentSignedUrl}
              poster={currentItem.previewUrl || undefined}
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
          ) : (
            <Box
              component="img"
              key={`${currentItem.id}:${currentBackgroundUrl}`}
              src={currentBackgroundUrl}
              alt={currentItem.name}
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
                display: { xs: "none", md: currentItem.kind === "image" ? "block" : "none" },
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
                display: { xs: "none", md: currentItem.kind === "image" ? "flex" : "none" },
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
                {items.map((item, index) => {
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
                        borderRadius: 1.5,
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
