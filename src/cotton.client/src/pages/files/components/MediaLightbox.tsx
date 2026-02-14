import React from "react";
import Lightbox from "yet-another-react-lightbox";
import "yet-another-react-lightbox/styles.css";
import "./MediaLightbox.css";
import Video from "yet-another-react-lightbox/plugins/video";
import Counter from "yet-another-react-lightbox/plugins/counter";
import Captions from "yet-another-react-lightbox/plugins/captions";
import Download from "yet-another-react-lightbox/plugins/download";
import Zoom from "yet-another-react-lightbox/plugins/zoom";
import Slideshow from "yet-another-react-lightbox/plugins/slideshow";
import Thumbnails from "yet-another-react-lightbox/plugins/thumbnails";
import Share from "yet-another-react-lightbox/plugins/share";
import "yet-another-react-lightbox/plugins/counter.css";
import "yet-another-react-lightbox/plugins/captions.css";
import "yet-another-react-lightbox/plugins/thumbnails.css";
import type { Slide } from "yet-another-react-lightbox";
import {
  Close,
  Share as ShareIcon,
  Pause as PauseIcon,
  Download as DownloadIcon,
  Slideshow as SlideshowIcon,
  ViewCarousel,
} from "@mui/icons-material";
import { useActivityDetection } from "../hooks/useActivityDetection";
import {
  isHeicFile,
  convertHeicToJpeg,
} from "../../../shared/utils/heicConverter";
import { formatBytes } from "../../../shared/utils/formatBytes";

const LOADING_PLACEHOLDER = `data:image/svg+xml,${encodeURIComponent(`
<svg xmlns="http://www.w3.org/2000/svg" width="120" height="120" viewBox="0 0 120 120">
  <style>
    .spinner { animation: spin 1s linear infinite; transform-origin: 60px 60px; }
    @keyframes spin { 100% { transform: rotate(360deg); } }
  </style>
  <circle class="spinner" cx="60" cy="20" r="8" fill="#888"/>
  <circle class="spinner" cx="60" cy="20" r="8" fill="#666" style="animation-delay: -0.875s"/>
  <circle class="spinner" cx="88.3" cy="31.7" r="8" fill="#888" style="animation-delay: -0.75s"/>
  <circle class="spinner" cx="100" cy="60" r="8" fill="#888" style="animation-delay: -0.625s"/>
  <circle class="spinner" cx="88.3" cy="88.3" r="8" fill="#888" style="animation-delay: -0.5s"/>
  <circle class="spinner" cx="60" cy="100" r="8" fill="#888" style="animation-delay: -0.375s"/>
  <circle class="spinner" cx="31.7" cy="88.3" r="8" fill="#888" style="animation-delay: -0.25s"/>
  <circle class="spinner" cx="20" cy="60" r="8" fill="#888" style="animation-delay: -0.125s"/>
  <circle class="spinner" cx="31.7" cy="31.7" r="8" fill="#888" style="animation-delay: 0s"/>
</svg>
`)}`;

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
}) => {
  const [index, setIndex] = React.useState(initialIndex);
  const thumbnailsRef = React.useRef(null);

  // Auto-hide controls after 2.5 seconds of inactivity
  const isActive = useActivityDetection(2500);

  const [originalUrls, setOriginalUrls] = React.useState<
    Record<string, string>
  >({});

  const loadingRef = React.useRef<Set<string>>(new Set());

  // Rebuild slides when originalUrls or shareUrls change
  const slides = React.useMemo(() => {
    return buildSlidesFromItems(items, originalUrls);
  }, [items, originalUrls]);

  const ensureSlideHasOriginal = React.useCallback(
    async (targetIndex: number) => {
      const item = items[targetIndex];
      if (!item) return;

      if (loadingRef.current.has(item.id)) return;

      setOriginalUrls((prev) => {
        if (prev[item.id]) return prev;

        loadingRef.current.add(item.id);

        (async () => {
          try {
            let url = await getSignedMediaUrl(item.id);

            if (item.kind === "image" && isHeicFile(item.name)) {
              url = await convertHeicToJpeg(url);
            }

            setOriginalUrls((p) => ({ ...p, [item.id]: url }));
          } catch (e) {
            console.error("Failed to load media original URL", e);
          } finally {
            loadingRef.current.delete(item.id);
          }
        })();

        return prev;
      });
    },
    [items, getSignedMediaUrl],
  );

  React.useEffect(() => {
    setIndex(initialIndex);
  }, [initialIndex]);

  React.useEffect(() => {
    if (!open) return;
    void ensureSlideHasOriginal(index);
  }, [open, index, ensureSlideHasOriginal]);

  // Build className based on activity state
  const lightboxClassName = [
    "lightbox-autohide",
    isActive ? "lightbox-autohide--active" : "lightbox-autohide--idle",
  ].join(" ");

  return (
    <Lightbox
      open={open}
      close={onClose}
      className={lightboxClassName}
      plugins={[
        Video,
        Zoom,
        Slideshow,
        Thumbnails,
        Download,
        Share,
        Counter,
        Captions,
      ]}
      slides={slides}
      index={index}
      on={{
        view: ({ index: currentIndex }) => {
          setIndex(currentIndex);
          void ensureSlideHasOriginal(currentIndex);
        },
      }}
      render={{
        buttonZoom: () => null,
        iconZoomIn: () => null,
        iconZoomOut: () => null,
        iconClose: () => <Close />,
        iconShare: () => <ShareIcon />,
        iconDownload: () => <DownloadIcon />,
        iconSlideshowPause: () => <PauseIcon />,
        iconSlideshowPlay: () => <SlideshowIcon />,
        iconThumbnailsVisible: () => <ViewCarousel />,
        iconThumbnailsHidden: () => <ViewCarousel />,
      }}
      captions={{
        descriptionTextAlign: "center",
        descriptionMaxLines: 1,
      }}
      zoom={{
        maxZoomPixelRatio: 8,
        zoomInMultiplier: 1,
        doubleTapDelay: 300,
        doubleClickDelay: 300,
        doubleClickMaxStops: 2,
        keyboardMoveDistance: 50,
        wheelZoomDistanceFactor: 500,
        pinchZoomDistanceFactor: 100,
        scrollToZoom: true,
      }}
      slideshow={{
        autoplay: false,
        delay: 5000,
      }}
      thumbnails={{
        ref: thumbnailsRef,
        position: "bottom",
        width: 120,
        height: 80,
        border: 0,
        borderRadius: 4,
        padding: 2,
        gap: 4,
        showToggle: true,
        hidden: true,
      }}
      video={{
        controls: true,
        playsInline: true,
        autoPlay: true,
      }}
      carousel={{
        finite: true,
        preload: 2,
        imageFit: "contain",
      }}
    />
  );
};

function buildSlidesFromItems(
  items: MediaItem[],
  originalUrls: Record<string, string>,
): Slide[] {
  return items.map<Slide>((item) => {
    const maybeOriginal = originalUrls[item.id];
    const sizeStr = item.sizeBytes ? formatBytes(item.sizeBytes) : "";
    const title = sizeStr ? `${sizeStr} • ${item.name}` : item.name;

    if (item.kind === "image") {
      const isLoading = !maybeOriginal && !item.previewUrl;
      const src = maybeOriginal || item.previewUrl || LOADING_PLACEHOLDER;
      return {
        type: "image",
        src,
        width: isLoading ? 120 : item.width,
        height: isLoading ? 120 : item.height,
        title,
        download: maybeOriginal
          ? { url: maybeOriginal, filename: item.name }
          : undefined,
        share: maybeOriginal
          ? {
              url: maybeOriginal,
              title: item.name,
            }
          : undefined,
      };
    }

    const poster = item.previewUrl || undefined;
    const src = maybeOriginal;

    if (!src) {
      return {
        type: "video",
        poster,
        width: item.width,
        height: item.height,
        title,
        share: undefined,
      } as Slide;
    }

    return {
      type: "video",
      poster,
      width: item.width,
      height: item.height,
      title,
      download: { url: src, filename: item.name },
      share: src
        ? {
            url: src,
            title: item.name,
          }
        : undefined,
      sources: [
        {
          src,
          type: item.mimeType,
        },
      ],
    } as Slide;
  });
}


