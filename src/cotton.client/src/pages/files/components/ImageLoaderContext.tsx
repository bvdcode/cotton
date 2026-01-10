import React, { createContext, useContext, useState, useCallback } from "react";
import { PhotoProvider } from "react-photo-view";
import { filesApi } from "../../../shared/api/filesApi";

type ImageUrlCache = Record<string, string>;
type ImageIdMap = Record<number, string>;

interface ImageLoaderContextType {
  getImageUrl: (nodeFileId: string, previewUrl: string) => string;
  preloadImage: (nodeFileId: string) => void;
  registerImage: (nodeFileId: string) => void;
}

const ImageLoaderContext = createContext<ImageLoaderContextType | null>(null);

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
    const nodeFileId = imageIds[index];
    if (nodeFileId) {
      preloadImage(nodeFileId);
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

export const useImageLoader = () => {
  const context = useContext(ImageLoaderContext);
  if (!context) {
    throw new Error("useImageLoader must be used within ImageLoaderProvider");
  }
  return context;
};
