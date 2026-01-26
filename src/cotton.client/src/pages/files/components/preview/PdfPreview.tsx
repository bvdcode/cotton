import {
  Box,
  CircularProgress,
  Typography,
  useMediaQuery,
} from "@mui/material";
import { useState, useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { filesApi } from "../../../../shared/api/filesApi";
import { useTheme as useMuiTheme } from "@mui/material/styles";
import {
  getDocument,
  GlobalWorkerOptions,
} from "pdfjs-dist/legacy/build/pdf.mjs";
import pdfWorker from "pdfjs-dist/legacy/build/pdf.worker.min.mjs?url";

interface PdfPreviewProps {
  fileId: string;
  fileName: string;
}

// Blob URL cache for PDFs
const blobUrlCache = new Map<string, string>();

export const PdfPreview = ({ fileId, fileName }: PdfPreviewProps) => {
  const { t } = useTranslation(["files", "common"]);
  const muiTheme = useMuiTheme();
  const isMobile = useMediaQuery(muiTheme.breakpoints.down("sm"));
  const cachedBlobUrl = blobUrlCache.get(fileId);
  const [blobUrl, setBlobUrl] = useState<string | null>(cachedBlobUrl ?? null);
  const [loading, setLoading] = useState(!cachedBlobUrl);
  const [loadingStage, setLoadingStage] = useState<
    "link" | "download" | "render"
  >("link");
  const [error, setError] = useState<string | null>(null);
  const [pdfBlob, setPdfBlob] = useState<Blob | null>(null);
  const [rendering, setRendering] = useState(false);
  const renderContainerRef = useRef<HTMLDivElement | null>(null);

  // Load PDF as blob on mount
  useEffect(() => {
    if (blobUrl && !isMobile) return;
    if (pdfBlob) return;

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

        if (!response.ok) {
          throw new Error(t("preview.errors.downloadFailed", { ns: "files" }));
        }

        // Step 3: Create blob URL
        // Blob URLs are not intercepted by React Router and work in iframe
        const blob = await response.blob();
        const url = URL.createObjectURL(blob);

        blobUrlCache.set(fileId, url);
        setBlobUrl(url);
        setPdfBlob(blob);
        setLoading(false);
      } catch {
        if (!cancelled) {
          setError(t("preview.errors.pdfLoadFailed", { ns: "files" }));
          setLoading(false);
        }
      }
    };

    loadPdf();

    return () => {
      cancelled = true;
    };
  }, [fileId, blobUrl, isMobile, pdfBlob, t]);

  // Cleanup blob URLs when component unmounts (but keep in cache for re-opening)
  // Note: We don't revoke cached URLs to allow reopening without re-download

  useEffect(() => {
    if (!isMobile || !pdfBlob) return;

    let cancelled = false;

    const renderPdf = async () => {
      setRendering(true);
      setLoadingStage("render");
      setError(null);

      try {
        GlobalWorkerOptions.workerSrc = pdfWorker;
        const data = await pdfBlob.arrayBuffer();
        const pdf = await getDocument({ data }).promise;

        if (cancelled) return;

        const container = renderContainerRef.current;
        if (!container) return;

        container.innerHTML = "";
        const containerWidth = container.clientWidth || window.innerWidth;

        for (let pageNumber = 1; pageNumber <= pdf.numPages; pageNumber += 1) {
          const page = await pdf.getPage(pageNumber);
          if (cancelled) return;

          const viewport = page.getViewport({ scale: 1 });
          const scale = containerWidth / viewport.width;
          const scaledViewport = page.getViewport({ scale });

          const canvas = document.createElement("canvas");
          canvas.className = "pdf-page-canvas";
          const context = canvas.getContext("2d");
          if (!context) {
            throw new Error(
              t("preview.errors.pdfDisplayFailed", { ns: "files" }),
            );
          }

          canvas.width = scaledViewport.width;
          canvas.height = scaledViewport.height;

          const renderTask = page.render({
            canvasContext: context,
            viewport: scaledViewport,
          });
          await renderTask.promise;

          if (cancelled) return;

          container.appendChild(canvas);
        }

        setRendering(false);
      } catch {
        if (!cancelled) {
          setError(t("preview.errors.pdfDisplayFailed", { ns: "files" }));
          setRendering(false);
        }
      }
    };

    void renderPdf();

    return () => {
      cancelled = true;
    };
  }, [isMobile, pdfBlob, t]);

  const handleLoad = () => {
    setLoading(false);
  };

  const handleError = () => {
    setLoading(false);
    setError(t("preview.errors.pdfDisplayFailed", { ns: "files" }));
  };

  return (
    <Box
      sx={{
        width: "100%",
        height: "100%",
        display: "flex",
        flexDirection: "column",
        position: "relative",
        py: 1,
      }}
    >
      {(loading || rendering) && (
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
            {loadingStage === "link"
              ? t("preview.pdf.loading.link", { ns: "files" })
              : loadingStage === "download"
                ? t("preview.pdf.loading.download", { ns: "files" })
                : t("preview.pdf.loading.render", { ns: "files" })}
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
      {!isMobile && blobUrl && (
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
      {isMobile && !error && (
        <Box
          ref={renderContainerRef}
          sx={{
            width: "100%",
            height: "100%",
            overflow: "auto",
            px: 1,
            pb: 2,
            display: loading || rendering ? "none" : "block",
            "& .pdf-page-canvas": {
              width: "100%",
              height: "auto",
              display: "block",
              borderRadius: 1,
              mb: 2,
            },
          }}
        />
      )}
    </Box>
  );
};
