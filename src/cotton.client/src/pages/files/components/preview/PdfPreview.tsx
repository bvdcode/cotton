import { Box, CircularProgress, Typography } from "@mui/material";
import { useState, useEffect } from "react";
import { filesApi } from "../../../../shared/api/filesApi";

interface PdfPreviewProps {
  fileId: string;
  fileName: string;
}

// Simple URL cache
const urlCache = new Map<string, string>();

export const PdfPreview = ({ fileId, fileName }: PdfPreviewProps) => {
  const [fileUrl, setFileUrl] = useState<string | null>(() => urlCache.get(fileId) ?? null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Load URL on mount
  useEffect(() => {
    if (fileUrl) {
      return;
    }

    let cancelled = false;
    
    filesApi.getDownloadLink(fileId, 60 * 24)
      .then((url) => {
        if (!cancelled) {
          urlCache.set(fileId, url);
          setFileUrl(url);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setError("Failed to load PDF link");
          setLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [fileId, fileUrl]);

  const handleLoad = () => {
    setLoading(false);
  };

  const handleError = () => {
    setLoading(false);
    setError("Failed to load PDF");
  };

  return (
    <Box
      sx={{
        width: "100%",
        height: "100%",
        display: "flex",
        flexDirection: "column",
        position: "relative",
      }}
    >
      {loading && (
        <Box
          sx={{
            position: "absolute",
            top: "50%",
            left: "50%",
            transform: "translate(-50%, -50%)",
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            gap: 2,
          }}
        >
          <CircularProgress />
          <Typography variant="body2" color="text.secondary">
            {fileUrl ? "Loading PDF..." : "Getting link..."}
          </Typography>
        </Box>
      )}
      {error && (
        <Box
          sx={{
            position: "absolute",
            top: "50%",
            left: "50%",
            transform: "translate(-50%, -50%)",
            textAlign: "center",
          }}
        >
          <Typography variant="body1" color="error">
            {error}
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
            {fileName}
          </Typography>
        </Box>
      )}
      {fileUrl && (
        <Box
          component="iframe"
          src={fileUrl}
          title={fileName}
          onLoad={handleLoad}
          onError={handleError}
          sx={{
            width: "100%",
            height: "100%",
            border: "none",
            display: loading || error ? "none" : "block",
          }}
        />
      )}
    </Box>
  );
};
