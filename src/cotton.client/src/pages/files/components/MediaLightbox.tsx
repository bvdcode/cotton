import React from "react";
import Lightbox from "yet-another-react-lightbox";
import "yet-another-react-lightbox/styles.css";
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

  const [originalUrls, setOriginalUrls] = React.useState<
    Record<string, string>
  >({});

  // Rebuild slides when originalUrls change
  const slides = React.useMemo(() => {
    return buildSlidesFromItems(items, originalUrls);
  }, [items, originalUrls]);

  const ensureSlideHasOriginal = React.useCallback(
    async (targetIndex: number) => {
      const item = items[targetIndex];
      if (!item) return;

      if (originalUrls[item.id]) return;

      try {
        const url = await getSignedMediaUrl(item.id);
        setOriginalUrls((prev) => ({ ...prev, [item.id]: url }));
      } catch (e) {
        console.error("Failed to load media original URL", e);
      }
    },
    [items, originalUrls, getSignedMediaUrl],
  );

  React.useEffect(() => {
    setIndex(initialIndex);
  }, [initialIndex]);

  React.useEffect(() => {
    if (!open) return;
    void ensureSlideHasOriginal(index);
  }, [open, index, ensureSlideHasOriginal]);

  return (
    <Lightbox
      open={open}
      close={onClose}
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
    const sizeStr = item.sizeBytes ? formatFileSize(item.sizeBytes) : "";
    const description = sizeStr ? `${item.name} | ${sizeStr}` : item.name;

    if (item.kind === "image") {
      const src = maybeOriginal ?? item.previewUrl;
      return {
        type: "image",
        src,
        width: item.width,
        height: item.height,
        description,
        download: maybeOriginal
          ? { url: maybeOriginal, filename: item.name }
          : undefined,
        share: {
          url: src,
          title: item.name,
        },
      };
    }

    const poster = item.previewUrl;
    const src = maybeOriginal;

    if (!src) {
      return {
        type: "video",
        poster,
        width: item.width,
        height: item.height,
        description,
        share: {
          url: poster,
          title: item.name,
        },
      } as Slide;
    }

    return {
      type: "video",
      poster,
      width: item.width,
      height: item.height,
      description,
      download: { url: src, filename: item.name },
      share: {
        url: src,
        title: item.name,
      },
      sources: [
        {
          src,
          type: item.mimeType,
        },
      ],
    } as Slide;
  });
}

function formatFileSize(bytes: number): string {
  if (bytes === 0) return "0 B";
  const k = 1024;
  const sizes = ["B", "KB", "MB", "GB"];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(2))} ${sizes[i]}`;
}
