import * as React from "react";
import type { Slide } from "yet-another-react-lightbox";
import {
  convertHeicToJpeg,
  isHeicFile,
} from "../../../shared/utils/heicConverter";
import { buildSlidesFromItems } from "../components/mediaLightboxSlides";
import type { MediaItem, SlideWithTitle } from "../components/mediaLightbox.types";

const PREVIEW_QUERY_PARAM = "preview";
const PREVIEW_QUERY_VALUE = "true";

const getPreviewQueryValue = (preferPreview: boolean): string => {
  return preferPreview ? PREVIEW_QUERY_VALUE : "false";
};

const applyPreviewModeToUrl = (url: string, preferPreview: boolean): string => {
  try {
    const parsed = new URL(url);
    parsed.searchParams.set(PREVIEW_QUERY_PARAM, getPreviewQueryValue(preferPreview));
    return parsed.toString();
  } catch {
    const [base, queryString = ""] = url.split("?");
    const searchParams = new URLSearchParams(queryString);

    searchParams.set(PREVIEW_QUERY_PARAM, getPreviewQueryValue(preferPreview));

    const nextQuery = searchParams.toString();
    return nextQuery ? `${base}?${nextQuery}` : base;
  }
};

interface UseMediaLightboxUrlsArgs {
  items: MediaItem[];
  getSignedMediaUrl: (id: string) => Promise<string>;
  getDownloadUrl?: (id: string) => Promise<string>;
  preferPreview: boolean;
}

export const useMediaLightboxUrls = ({
  items,
  getSignedMediaUrl,
  getDownloadUrl,
  preferPreview,
}: UseMediaLightboxUrlsArgs) => {
  const [signedUrls, setSignedUrls] = React.useState<Record<string, string>>({});
  const [displayUrls, setDisplayUrls] = React.useState<Record<string, string>>({});
  const [downloadUrls, setDownloadUrls] = React.useState<Record<string, string>>({});

  const downloadUrlsRef = React.useRef<Record<string, string>>({});
  const inFlightDownloadLoadsRef = React.useRef<Map<string, Promise<string | null>>>(
    new Map(),
  );
  const inFlightOriginalLoadsRef = React.useRef<Map<string, Promise<string | null>>>(
    new Map(),
  );
  const inFlightHeicFallbacksRef = React.useRef<Map<string, Promise<string | null>>>(
    new Map(),
  );
  const loadingRef = React.useRef<Set<string>>(new Set());
  const loadedIdsRef = React.useRef<Set<string>>(new Set());
  const requestVersionRef = React.useRef<number>(0);

  React.useEffect(() => {
    downloadUrlsRef.current = downloadUrls;
  }, [downloadUrls]);

  React.useEffect(() => {
    requestVersionRef.current += 1;

    setDisplayUrls((previous) => {
      if (Object.keys(previous).length === 0) {
        return previous;
      }

      const updated: Record<string, string> = {};
      for (const [fileId, currentUrl] of Object.entries(previous)) {
        updated[fileId] = currentUrl.startsWith("blob:")
          ? currentUrl
          : applyPreviewModeToUrl(currentUrl, preferPreview);
      }

      return updated;
    });

    loadingRef.current.clear();
    loadedIdsRef.current.clear();
    inFlightOriginalLoadsRef.current.clear();
    inFlightHeicFallbacksRef.current.clear();
  }, [preferPreview]);

  const slides = React.useMemo(() => {
    return buildSlidesFromItems(items, displayUrls, signedUrls);
  }, [items, displayUrls, signedUrls]);

  const ensureOriginalUrl = React.useCallback(
    async (item: MediaItem): Promise<string | null> => {
      const existingDisplayUrl = displayUrls[item.id];
      if (existingDisplayUrl) {
        return existingDisplayUrl;
      }

      const existingInFlight = inFlightOriginalLoadsRef.current.get(item.id);
      if (existingInFlight) {
        return await existingInFlight;
      }

      const loadTask = (async () => {
        const requestVersion = requestVersionRef.current;

        try {
          const baseSignedUrl = await getSignedMediaUrl(item.id);

          if (requestVersionRef.current !== requestVersion) {
            return null;
          }

          if (item.kind === "video") {
            setSignedUrls((prev) => ({ ...prev, [item.id]: baseSignedUrl }));
            return baseSignedUrl;
          }

          const displayUrl = applyPreviewModeToUrl(baseSignedUrl, preferPreview);

          setDisplayUrls((prev) =>
            prev[item.id] ? prev : { ...prev, [item.id]: displayUrl },
          );

          return displayUrl;
        } catch (error) {
          console.error("Failed to load media URL", error);
          return null;
        } finally {
          inFlightOriginalLoadsRef.current.delete(item.id);
        }
      })();

      inFlightOriginalLoadsRef.current.set(item.id, loadTask);
      return await loadTask;
    },
    [displayUrls, getSignedMediaUrl, preferPreview],
  );

  const ensureSlideHasOriginal = React.useCallback(
    async (targetIndex: number) => {
      const item = items[targetIndex];
      if (!item) return;

      if (loadedIdsRef.current.has(item.id) || loadingRef.current.has(item.id)) {
        return;
      }

      loadingRef.current.add(item.id);

      try {
        await ensureOriginalUrl(item);
      } finally {
        loadingRef.current.delete(item.id);
        loadedIdsRef.current.add(item.id);
      }
    },
    [ensureOriginalUrl, items],
  );

  const handleSlideImageError = React.useCallback(
    async (slide: Slide): Promise<void> => {
      const lightboxSlide = slide as SlideWithTitle;
      const item = items.find((entry) => entry.id === lightboxSlide.fileId);
      if (!item || item.kind !== "image" || !isHeicFile(item.name)) {
        return;
      }

      const currentDisplayUrl = displayUrls[item.id];
      if (currentDisplayUrl?.startsWith("blob:")) {
        return;
      }

      const existingFallback = inFlightHeicFallbacksRef.current.get(item.id);
      if (existingFallback) {
        await existingFallback;
        return;
      }

      const fallbackTask = (async () => {
        const requestVersion = requestVersionRef.current;

        try {
          const originalUrl = (await ensureOriginalUrl(item)) ?? currentDisplayUrl;
          if (!originalUrl) {
            return null;
          }

          const convertedUrl = await convertHeicToJpeg(originalUrl);
          if (requestVersionRef.current !== requestVersion) {
            return null;
          }

          setDisplayUrls((prev) => ({ ...prev, [item.id]: convertedUrl }));
          return convertedUrl;
        } catch (error) {
          console.error("Failed to convert HEIC after image load error", error);
          return null;
        } finally {
          inFlightHeicFallbacksRef.current.delete(item.id);
        }
      })();

      inFlightHeicFallbacksRef.current.set(item.id, fallbackTask);
      await fallbackTask;
    },
    [displayUrls, ensureOriginalUrl, items],
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

  const resolveSlideDownloadUrl = React.useCallback(
    async (slide: Slide): Promise<string | null> => {
      const lightboxSlide = slide as SlideWithTitle;
      const resolved = await ensureDownloadUrl(lightboxSlide.fileId);
      if (resolved) {
        return resolved;
      }
      return getSlideSourceUrl(lightboxSlide);
    },
    [ensureDownloadUrl, getSlideSourceUrl],
  );

  return {
    slides,
    ensureSlideHasOriginal,
    handleSlideImageError,
    resolveSlideDownloadUrl,
  };
};
