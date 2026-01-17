import React from "react";
import Lightbox from "yet-another-react-lightbox";
import "yet-another-react-lightbox/styles.css";

import Video from "yet-another-react-lightbox/plugins/video";
import Fullscreen from "yet-another-react-lightbox/plugins/fullscreen";
import Counter from "yet-another-react-lightbox/plugins/counter";
import Captions from "yet-another-react-lightbox/plugins/captions";
import Download from "yet-another-react-lightbox/plugins/download";
import "yet-another-react-lightbox/plugins/counter.css";
import "yet-another-react-lightbox/plugins/captions.css";

import type { Slide } from "yet-another-react-lightbox";

type MediaKind = "image" | "video";

export interface MediaItem {
  id: string;
  kind: MediaKind;
  name: string;
  previewUrl: string;
  width?: number;
  height?: number;
  mimeType?: string;
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
 * MediaLightbox
 * - показывает элементы items как слайды (фото + видео)
 * - использует previewUrl как src/poster
 * - при первом показе конкретного слайда запрашивает signed URL и подменяет src
 */
export const MediaLightbox: React.FC<MediaLightboxProps> = ({
  items,
  open,
  initialIndex,
  onClose,
  getSignedMediaUrl,
}) => {
  const [index, setIndex] = React.useState(initialIndex);

  // map: mediaId -> оригинальный URL (подписанный)
  const [originalUrls, setOriginalUrls] = React.useState<Record<string, string>>(
    {}
  );

  // Rebuild slides when originalUrls change
  const slides = React.useMemo(() => {
    return buildSlidesFromItems(items, originalUrls);
  }, [items, originalUrls]);

  // Reset index when items change
  React.useEffect(() => {
    setIndex(initialIndex);
  }, [initialIndex]);

  // следим за сменой open -> при открытии можно сразу подгрузить текущий слайд
  React.useEffect(() => {
    if (!open) return;
    // подстрахуемся — загрузим текущий слайд
    void ensureSlideHasOriginal(index);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, index]);

  async function ensureSlideHasOriginal(targetIndex: number) {
    const item = items[targetIndex];
    if (!item) return;

    // если уже есть оригинал — выходим
    if (originalUrls[item.id]) return;

    try {
      const url = await getSignedMediaUrl(item.id);
      // Просто обновляем кэш URL - slides пересоберутся автоматически через useMemo
      setOriginalUrls((prev) => ({ ...prev, [item.id]: url }));
    } catch (e) {
      // eslint-disable-next-line no-console
      console.error("Failed to load media original URL", e);
    }
  }

  return (
    <Lightbox
      open={open}
      close={onClose}
      plugins={[Video, Fullscreen, Counter, Captions, Download]}
      slides={slides}
      index={index}
      on={{
        view: ({ index: currentIndex }) => {
          setIndex(currentIndex);
          void ensureSlideHasOriginal(currentIndex);
        },
      }}
      // дефолтные настройки видео
      video={{
        controls: true,
        playsInline: true,
        autoPlay: true,
      }}
      // можно подкрутить карусель, если надо
      carousel={{
        finite: false,
        preload: 2,
        imageFit: "contain",
      }}
    />
  );
};

// вспомогательная функция, создает слайды на основе превью и уже известных оригиналов
function buildSlidesFromItems(
  items: MediaItem[],
  originalUrls: Record<string, string>
): Slide[] {
  return items.map<Slide>((item) => {
    const maybeOriginal = originalUrls[item.id];
    const description = item.sizeBytes
      ? formatFileSize(item.sizeBytes)
      : undefined;

    if (item.kind === "image") {
      const src = maybeOriginal ?? item.previewUrl;
      return {
        type: "image",
        src,
        width: item.width,
        height: item.height,
        title: item.name,
        description,
        download: maybeOriginal
          ? { url: maybeOriginal, filename: item.name }
          : undefined,
      };
    }

    // video
    const poster = item.previewUrl;
    const src = maybeOriginal;

    if (!src) {
      // на старте показываем только постер, без video-сорца
      return {
        type: "video",
        poster,
        width: item.width,
        height: item.height,
        title: item.name,
        description,
      } as Slide;
    }

    return {
      type: "video",
      poster,
      width: item.width,
      height: item.height,
      title: item.name,
      description,
      download: { url: src, filename: item.name },
      sources: [
        {
          src,
          type: item.mimeType ?? "video/mp4",
        },
      ],
    } as Slide;
  });
}

// Helper to format file size
function formatFileSize(bytes: number): string {
  if (bytes === 0) return "0 B";
  const k = 1024;
  const sizes = ["B", "KB", "MB", "GB"];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(2))} ${sizes[i]}`;
}
