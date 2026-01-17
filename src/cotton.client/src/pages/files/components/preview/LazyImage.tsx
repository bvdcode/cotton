import { useState, useEffect } from "react";
import { Box, CircularProgress } from "@mui/material";
import { filesApi } from "../../../../shared/api/filesApi";

interface LazyImageRenderProps {
  attrs: React.HTMLAttributes<HTMLDivElement> & { style?: React.CSSProperties };
  scale: number;
}

// Cache for loaded URLs (persists across renders)
const urlCache = new Map<string, string>();
const loadingPromises = new Map<string, Promise<string>>();

// Get or load URL with caching
const getOrLoadUrl = async (fileId: string): Promise<string> => {
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

// Component that loads and displays image lazily
export const LazyImageContent: React.FC<{
  fileId: string;
  fileName: string;
}> = ({ fileId, fileName }) => {
  const [url, setUrl] = useState<string | null>(() => urlCache.get(fileId) ?? null);
  const [loading, setLoading] = useState(!url);
  const [error, setError] = useState(false);

  useEffect(() => {
    if (url) return;

    let cancelled = false;
    setLoading(true);

    getOrLoadUrl(fileId)
      .then((loadedUrl) => {
        if (!cancelled) {
          setUrl(loadedUrl);
          setLoading(false);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setError(true);
          setLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [fileId, url]);

  if (loading) {
    return (
      <Box
        sx={{
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          width: "100%",
          height: "100%",
          minHeight: 200,
          bgcolor: "rgba(0,0,0,0.8)",
        }}
      >
        <CircularProgress sx={{ color: "white" }} />
      </Box>
    );
  }

  if (error || !url) {
    return (
      <Box
        sx={{
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          width: "100%",
          height: "100%",
          minHeight: 200,
          bgcolor: "rgba(0,0,0,0.8)",
          color: "white",
        }}
      >
        Failed to load {fileName}
      </Box>
    );
  }

  return (
    <img
      src={url}
      alt={fileName}
      style={{
        display: "block",
        maxWidth: "100vw",
        maxHeight: "90vh",
        objectFit: "contain",
      }}
    />
  );
};

// Render function for PhotoView
export const renderLazyImage = (fileId: string, fileName: string) => {
  return ({ attrs }: LazyImageRenderProps) => {
    const { style, ...rest } = attrs;
    return (
      <div
        {...rest}
        style={{
          ...style,
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
        }}
      >
        <LazyImageContent fileId={fileId} fileName={fileName} />
      </div>
    );
  };
};

// Check if URL is already cached (for src prop optimization)
export const getCachedUrl = (fileId: string): string | undefined => {
  return urlCache.get(fileId);
};
