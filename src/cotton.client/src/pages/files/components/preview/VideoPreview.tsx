import { useState, useEffect } from "react";
import { Box, CircularProgress } from "@mui/material";
import { filesApi } from "../../../../shared/api/filesApi";

interface VideoPreviewProps {
  fileUrl: string;
  fileName: string;
}

interface PhotoRenderParams {
  attrs: React.HTMLAttributes<HTMLDivElement> & { style?: React.CSSProperties };
  scale: number;
}

// Shared cache with LazyImage
const urlCache = new Map<string, string>();
const loadingPromises = new Map<string, Promise<string>>();

const getOrLoadUrl = async (fileId: string): Promise<string> => {
  const cached = urlCache.get(fileId);
  if (cached) return cached;

  const existingPromise = loadingPromises.get(fileId);
  if (existingPromise) return existingPromise;

  const promise = filesApi.getDownloadLink(fileId, 60 * 24).then((url) => {
    urlCache.set(fileId, url);
    loadingPromises.delete(fileId);
    return url;
  });

  loadingPromises.set(fileId, promise);
  return promise;
};

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
const LazyVideoContent: React.FC<{
  fileId: string;
  fileName: string;
  width: number;
  height: number;
}> = ({ fileId, fileName, width, height }) => {
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

// Render function for PhotoView custom render - now with lazy loading
export const renderVideoPreview = (fileId: string, fileName: string) => {
  return ({ attrs, scale }: PhotoRenderParams) => {
    const width = attrs.style?.width
      ? parseFloat(attrs.style.width as string)
      : VIDEO_WIDTH;
    const offset = (width - VIDEO_WIDTH) / VIDEO_WIDTH;
    const childScale = scale === 1 ? scale + offset : 1 + offset;

    return (
      <div {...attrs}>
        <div
          style={{
            width: VIDEO_WIDTH,
            height: VIDEO_HEIGHT,
            transformOrigin: "0 0",
            transform: `scale(${childScale})`,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
          }}
        >
          <LazyVideoContent
            fileId={fileId}
            fileName={fileName}
            width={VIDEO_WIDTH}
            height={VIDEO_HEIGHT}
          />
        </div>
      </div>
    );
  };
};
