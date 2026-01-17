import { filesApi } from "../../../../shared/api/filesApi";

// Cache for loaded URLs (persists across renders)
export const urlCache = new Map<string, string>();
const loadingPromises = new Map<string, Promise<string>>();

// Get or load URL with caching
export const getOrLoadUrl = async (fileId: string): Promise<string> => {
  // Check cache first
  const cached = urlCache.get(fileId);
  if (cached) return cached;

  // Check if already loading
  const existingPromise = loadingPromises.get(fileId);
  if (existingPromise) return existingPromise;

  // Start new load
  const promise = filesApi.getDownloadLink(fileId, 60 * 24).then((url) => {
    urlCache.set(fileId, url);
    loadingPromises.delete(fileId);
    return url;
  });

  loadingPromises.set(fileId, promise);
  return promise;
};

// Check if URL is already cached (for src prop optimization)
export const getCachedUrl = (fileId: string): string | undefined => {
  return urlCache.get(fileId);
};
