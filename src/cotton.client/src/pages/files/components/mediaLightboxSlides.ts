import type { SlideWithTitle, MediaItem } from "./mediaLightbox.types";
import { formatBytes } from "../../../shared/utils/formatBytes";

export const TRANSPARENT_PLACEHOLDER =
  "data:image/gif;base64,R0lGODlhAQABAAAAACH5BAEKAAEALAAAAAABAAEAAAICTAEAOw==";

export function buildSlidesFromItems(
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

  return items.map<SlideWithTitle>((item, idx) => {
    const position = idx + 1;
    const title = buildTitle(position, item);
    const signedUrl = signedUrls[item.id] ?? null;
    const displayUrl = displayUrls[item.id] ?? null;

    if (item.kind === "image") {
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
    }

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
  });
}
