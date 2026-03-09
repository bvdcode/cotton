import * as React from "react";
import type { Slide } from "yet-another-react-lightbox";
import {
  convertHeicToJpeg,
  isHeicFile,
} from "../../../shared/utils/heicConverter";
import { buildSlidesFromItems } from "../components/mediaLightboxSlides";
import type { MediaItem, SlideWithTitle } from "../components/mediaLightbox.types";

const IMAGE_DECODE_TIMEOUT_MS = 400;

interface UseMediaLightboxUrlsArgs {
  items: MediaItem[];
  getSignedMediaUrl: (id: string) => Promise<string>;
  getDownloadUrl?: (id: string) => Promise<string>;
}

export const useMediaLightboxUrls = ({
  items,
  getSignedMediaUrl,
  getDownloadUrl,
}: UseMediaLightboxUrlsArgs) => {
  const [signedUrls, setSignedUrls] = React.useState<Record<string, string>>({});
  const [displayUrls, setDisplayUrls] = React.useState<Record<string, string>>({});
  const [downloadUrls, setDownloadUrls] = React.useState<Record<string, string>>({});

  const downloadUrlsRef = React.useRef<Record<string, string>>({});
  const inFlightDownloadLoadsRef = React.useRef<Map<string, Promise<string | null>>>(
    new Map(),
  );
  const loadingRef = React.useRef<Set<string>>(new Set());
  const loadedIdsRef = React.useRef<Set<string>>(new Set());

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
            await Promise.race<void | undefined>([
              image.decode(),
              new Promise<void>((decodeResolve) => {
                window.setTimeout(() => {
                  decodeResolve();
                }, IMAGE_DECODE_TIMEOUT_MS);
              }),
            ]);
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

      if (loadedIdsRef.current.has(item.id) || loadingRef.current.has(item.id)) {
        return;
      }

      loadingRef.current.add(item.id);

      try {
        const signedUrl = await getSignedMediaUrl(item.id);

        if (item.kind === "video") {
          setSignedUrls((prev) => ({ ...prev, [item.id]: signedUrl }));
        }

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

        setDisplayUrls((prev) => ({ ...prev, [item.id]: nextDisplayUrl }));
      } catch (error) {
        console.error("Failed to load media original URL", error);
      } finally {
        loadingRef.current.delete(item.id);
        loadedIdsRef.current.add(item.id);
      }
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
    resolveSlideDownloadUrl,
  };
};
