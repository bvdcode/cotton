import { Box, CircularProgress, Typography } from "@mui/material";
import { useState, useEffect } from "react";
import { filesApi } from "../../../../shared/api/filesApi";

interface PdfPreviewProps {
  fileId: string;
  fileName: string;
}

// Blob URL cache for PDFs
const blobUrlCache = new Map<string, string>();

export const PdfPreview = ({ fileId, fileName }: PdfPreviewProps) => {
  const cachedBlobUrl = blobUrlCache.get(fileId);
  const [blobUrl, setBlobUrl] = useState<string | null>(cachedBlobUrl ?? null);
  const [loading, setLoading] = useState(!cachedBlobUrl);
  const [loadingStage, setLoadingStage] = useState<"link" | "download">("link");
  const [error, setError] = useState<string | null>(null);

  // Load PDF as blob on mount
  useEffect(() => {
    if (blobUrl) return;

    let cancelled = false;

    const loadPdf = async () => {
      try {
        // Step 1: Get download link with download=false for inline
        setLoadingStage("link");
        const downloadUrl = await filesApi.getDownloadLink(fileId, 60 * 24);

        if (cancelled) return;

        // Step 2: Fetch as blob to avoid React Router intercepting the URL
        // This is important for production builds where /api/* might be caught by routing
        setLoadingStage("download");
        const fullUrl = downloadUrl.startsWith("http")
          ? downloadUrl
          : `${window.location.origin}${downloadUrl}`;
        const previewUrl =
          fullUrl + (fullUrl.includes("?") ? "&" : "?") + "download=false";

        const response = await fetch(previewUrl);

        if (cancelled) return;

        if (!response.ok) throw new Error("Download failed");

        // Step 3: Create blob URL
        // Blob URLs are not intercepted by React Router and work in iframe
        const blob = await response.blob();
        const url = URL.createObjectURL(blob);

        blobUrlCache.set(fileId, url);
        setBlobUrl(url);
        setLoading(false);
      } catch {
        if (!cancelled) {
          setError("Failed to load PDF");
          setLoading(false);
        }
      }
    };

    loadPdf();

    return () => {
      cancelled = true;
    };
  }, [fileId, blobUrl]);

  // Cleanup blob URLs when component unmounts (but keep in cache for re-opening)
  // Note: We don't revoke cached URLs to allow reopening without re-download

  const handleLoad = () => {
    setLoading(false);
  };

  const handleError = () => {
    setLoading(false);
    setError("Failed to display PDF");
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
            {loadingStage === "link" ? "Getting link..." : "Downloading PDF..."}
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
      {blobUrl && (
        <Box
          component="iframe"
          src={blobUrl}
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
