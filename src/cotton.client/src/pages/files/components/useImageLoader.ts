import { useContext } from "react";
import { ImageLoaderContext } from "./ImageLoaderContext";

export const useImageLoader = () => {
  const context = useContext(ImageLoaderContext);
  if (!context) {
    throw new Error("useImageLoader must be used within ImageLoaderProvider");
  }
  return context;
};
