import { useState, useEffect } from "react";
import { Box, CircularProgress } from "@mui/material";
import { urlCache, getOrLoadUrl } from "./lazyLoadUtils";

interface VideoPreviewProps {
  fileUrl: string;
  fileName: string;
}

export const VideoPreview: React.FC<VideoPreviewProps> = ({
  fileUrl,
  fileName,
}) => {
  return (
    <Box
      component="video"
      controls
      autoPlay
      sx={{
        maxWidth: "100%",
        maxHeight: "80vh",
        outline: "none",
      }}
    >
      <source src={fileUrl} />
      {fileName}
    </Box>
  );
};

// Video dimensions for gallery
export const VIDEO_WIDTH = 960;
export const VIDEO_HEIGHT = 540;

// Lazy video component
export const LazyVideoContent: React.FC<{
  fileId: string;
  fileName: string;
  width: number;
  height: number;
}> = ({ fileId, fileName, width, height }) => {
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
          width,
          height,
          bgcolor: "#000",
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
          width,
          height,
          bgcolor: "#000",
          color: "white",
        }}
      >
        Failed to load {fileName}
      </Box>
    );
  }

  return (
    <video
      key={fileId}
      controls
      autoPlay
      style={{
        width,
        height,
        outline: "none",
        backgroundColor: "#000",
      }}
      onMouseDown={(e) => e.stopPropagation()}
    >
      <source src={url} type="video/mp4" />
      <source src={url} type="video/webm" />
      <source src={url} type="video/ogg" />
      {fileName}
    </video>
  );
};
