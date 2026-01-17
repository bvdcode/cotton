import { useState, useEffect } from "react";
import { Box, CircularProgress } from "@mui/material";
import { urlCache, getOrLoadUrl } from "./lazyLoadUtils";

// Component that loads and displays image lazily
export const LazyImageContent: React.FC<{
  fileId: string;
  fileName: string;
}> = ({ fileId, fileName }) => {
  const cachedUrl = urlCache.get(fileId);
  const [url, setUrl] = useState<string | null>(cachedUrl ?? null);
  const [loading, setLoading] = useState(!cachedUrl);
  const [error, setError] = useState(false);

  useEffect(() => {
    if (url) return;

    let cancelled = false;

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
