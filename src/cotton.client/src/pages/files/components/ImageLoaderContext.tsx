import { createContext } from "react";

export interface ImageLoaderContextType {
  getImageUrl: (nodeFileId: string, previewUrl: string) => string;
  preloadImage: (nodeFileId: string) => void;
  registerImage: (nodeFileId: string) => void;
}

export const ImageLoaderContext = createContext<ImageLoaderContextType | null>(null);
