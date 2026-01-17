import React, { useState, useCallback } from "react";
import { filesApi } from "../../../shared/api/filesApi";
import { ImageLoaderContext } from "./ImageLoaderContext";

type UrlCache = Record<string, string>;

export const ImageLoaderProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [cache, setCache] = useState<UrlCache>({});
  const [loading, setLoading] = useState<Set<string>>(new Set());
  const [mediaIds, setMediaIds] = useState<string[]>([]);

  const preloadImage = useCallback(async (nodeFileId: string) => {
    if (cache[nodeFileId] || loading.has(nodeFileId)) return;

    setLoading(prev => new Set(prev).add(nodeFileId));

    try {
      const url = await filesApi.getDownloadLink(nodeFileId, 60 * 24);
      setCache(prev => ({ ...prev, [nodeFileId]: url }));
    } catch (error) {
      console.error("Failed to preload media:", error);
    } finally {
      setLoading(prev => {
        const next = new Set(prev);
        next.delete(nodeFileId);
        return next;
      });
    }
  }, [cache, loading]);

  const getImageUrl = useCallback((nodeFileId: string, previewUrl: string): string => {
    return cache[nodeFileId] ?? previewUrl;
  }, [cache]);

  const getMediaUrl = useCallback((nodeFileId: string): string | null => {
    return cache[nodeFileId] ?? null;
  }, [cache]);

  const registerImage = useCallback((nodeFileId: string) => {
    setMediaIds(prev => {
      if (!prev.includes(nodeFileId)) {
        return [...prev, nodeFileId];
      }
      return prev;
    });
  }, []);

  const registerMedia = useCallback((nodeFileId: string) => {
    setMediaIds(prev => {
      if (!prev.includes(nodeFileId)) {
        return [...prev, nodeFileId];
      }
      return prev;
    });
    // Start preloading immediately for media files
    void preloadImage(nodeFileId);
  }, [preloadImage]);

  const handleIndexChange = useCallback((index: number) => {
    const currentId = mediaIds[index];
    const nextId = mediaIds[index + 1];
    
    if (currentId) {
      void preloadImage(currentId);
    }
    if (nextId) {
      void preloadImage(nextId);
    }
  }, [mediaIds, preloadImage]);

  return (
    <ImageLoaderContext.Provider value={{
      getImageUrl,
      preloadImage,
      registerImage,
      registerMedia,
      getMediaUrl,
      handleIndexChange,
    }}>
      {children}
    </ImageLoaderContext.Provider>
  );
};