import React, { useState, useCallback } from "react";
import { PhotoProvider } from "react-photo-view";
import { filesApi } from "../../../shared/api/filesApi";
import { ImageLoaderContext } from "./ImageLoaderContext";

type ImageUrlCache = Record<string, string>;

export const ImageLoaderProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [cache, setCache] = useState<ImageUrlCache>({});
  const [loading, setLoading] = useState<Set<string>>(new Set());
  const [imageIds, setImageIds] = useState<string[]>([]);

  const preloadImage = useCallback(async (nodeFileId: string) => {
    if (cache[nodeFileId] || loading.has(nodeFileId)) return;

    setLoading(prev => new Set(prev).add(nodeFileId));

    try {
      const url = await filesApi.getDownloadLink(nodeFileId, 60 * 24);
      setCache(prev => ({ ...prev, [nodeFileId]: url }));
    } catch (error) {
      console.error("Failed to preload image:", error);
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

  const registerImage = useCallback((nodeFileId: string) => {
    setImageIds(prev => {
      if (!prev.includes(nodeFileId)) {
        return [...prev, nodeFileId];
      }
      return prev;
    });
  }, []);

  const handleIndexChange = useCallback((index: number) => {
    const currentId = imageIds[index];
    const nextId = imageIds[index + 1];
    
    if (currentId) {
      void preloadImage(currentId);
    }
    if (nextId) {
      void preloadImage(nextId);
    }
  }, [imageIds, preloadImage]);

  return (
    <ImageLoaderContext.Provider value={{ getImageUrl, preloadImage, registerImage }}>
      <PhotoProvider onIndexChange={handleIndexChange}>
        {children}
      </PhotoProvider>
    </ImageLoaderContext.Provider>
  );
};