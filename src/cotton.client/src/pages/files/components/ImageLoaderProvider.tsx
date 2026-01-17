import React, { useState, useCallback, useRef } from "react";
import { filesApi } from "../../../shared/api/filesApi";
import { ImageLoaderContext } from "./ImageLoaderContext";

type UrlCache = Record<string, string>;

export const ImageLoaderProvider: React.FC<{ children: React.ReactNode }> = ({
  children,
}) => {
  const [cache, setCache] = useState<UrlCache>({});
  const loadingRef = useRef<Set<string>>(new Set());
  // Store ordered list of media IDs as they are registered
  const mediaIdsRef = useRef<string[]>([]);

  const preloadImage = useCallback((nodeFileId: string) => {
    // Use ref to avoid stale closure issues
    if (loadingRef.current.has(nodeFileId)) return;

    // Check if already cached
    setCache((currentCache) => {
      if (currentCache[nodeFileId]) {
        return currentCache; // Already cached, no change
      }

      // Not cached and not loading - start loading
      if (!loadingRef.current.has(nodeFileId)) {
        loadingRef.current.add(nodeFileId);

        filesApi
          .getDownloadLink(nodeFileId, 60 * 24)
          .then((url) => {
            setCache((prev) => ({ ...prev, [nodeFileId]: url }));
          })
          .catch((error) => {
            console.error("Failed to preload media:", error);
          })
          .finally(() => {
            loadingRef.current.delete(nodeFileId);
          });
      }

      return currentCache;
    });
  }, []);

  const getImageUrl = useCallback(
    (nodeFileId: string, previewUrl: string): string => {
      return cache[nodeFileId] ?? previewUrl;
    },
    [cache],
  );

  const getMediaUrl = useCallback(
    (nodeFileId: string): string | null => {
      return cache[nodeFileId] ?? null;
    },
    [cache],
  );

  const registerImage = useCallback(
    (nodeFileId: string) => {
      if (!mediaIdsRef.current.includes(nodeFileId)) {
        mediaIdsRef.current = [...mediaIdsRef.current, nodeFileId];
      }
    },
    [],
  );

  const registerMedia = useCallback(
    (nodeFileId: string) => {
      if (!mediaIdsRef.current.includes(nodeFileId)) {
        mediaIdsRef.current = [...mediaIdsRef.current, nodeFileId];
      }
      // Start preloading immediately for media files
      preloadImage(nodeFileId);
    },
    [preloadImage],
  );

  // This is called by PhotoProvider with the actual gallery index
  // We use the images array from PhotoProvider state to get correct IDs
  const handleIndexChange = useCallback(
    (index: number, state: { images: Array<{ key?: string | number }> }) => {
      const images = state?.images ?? [];
      
      // Get current, next and previous image keys (which are file IDs)
      const currentKey = images[index]?.key;
      const nextKey = images[index + 1]?.key;
      const prevKey = images[index - 1]?.key;

      // Preload current, next and previous
      if (currentKey && typeof currentKey === "string") {
        preloadImage(currentKey);
      }
      if (nextKey && typeof nextKey === "string") {
        preloadImage(nextKey);
      }
      if (prevKey && typeof prevKey === "string") {
        preloadImage(prevKey);
      }
    },
    [preloadImage],
  );

  return (
    <ImageLoaderContext.Provider
      value={{
        getImageUrl,
        preloadImage,
        registerImage,
        registerMedia,
        getMediaUrl,
        handleIndexChange,
      }}
    >
      {children}
    </ImageLoaderContext.Provider>
  );
};