import { Box, CircularProgress, Typography } from "@mui/material";
import { useState, useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { filesApi } from "../../../../shared/api/filesApi";
import { previewConfig } from "../../../../shared/config/previewConfig";
import { formatBytes } from "../../../../shared/utils/formatBytes";
import "pdfjs-dist/web/pdf_viewer.css";
import {
  getDocument,
  GlobalWorkerOptions,
  TextLayer,
} from "pdfjs-dist/legacy/build/pdf.mjs";
import pdfWorker from "pdfjs-dist/legacy/build/pdf.worker.min.mjs?url";

interface PdfPreviewProps {
  fileId: string;
  fileName: string;
  fileSizeBytes?: number | null;
}

// Blob URL cache for PDFs
const blobUrlCache = new Map<string, string>();

export const PdfPreview = ({
  fileId,
  fileName,
  fileSizeBytes,
}: PdfPreviewProps) => {
  const { t } = useTranslation(["files", "common"]);
  const isMobile =
    typeof navigator !== "undefined" &&
    /Android|iPhone|iPad|iPod/i.test(navigator.userAgent);

  const isTooLarge =
    typeof fileSizeBytes === "number" &&
    fileSizeBytes > previewConfig.MAX_PDF_PREVIEW_SIZE_BYTES;
  const maxMB = previewConfig.MAX_PDF_PREVIEW_SIZE_BYTES / (1024 * 1024);

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
  const [forcePdfJs, setForcePdfJs] = useState(isMobile);
  const shouldUsePdfJs = forcePdfJs;

  // Load PDF as blob on mount
  useEffect(() => {
    if (isTooLarge) return;
    if (blobUrl && !shouldUsePdfJs) return;
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
  }, [blobUrl, fileId, isTooLarge, pdfBlob, shouldUsePdfJs, t]);

  // Cleanup blob URLs when component unmounts (but keep in cache for re-opening)
  // Note: We don't revoke cached URLs to allow reopening without re-download

  useEffect(() => {
    if (isTooLarge) return;
    if (!shouldUsePdfJs || !pdfBlob) return;

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
        if (!container) {
          setRendering(false);
          return;
        }

        container.innerHTML = "";
        const style = window.getComputedStyle(container);
        const paddingLeft = Number.parseFloat(style.paddingLeft) || 0;
        const paddingRight = Number.parseFloat(style.paddingRight) || 0;
        const measuredWidth = container.clientWidth;
        const availableWidth =
          (measuredWidth > 0 ? measuredWidth : window.innerWidth) -
          paddingLeft -
          paddingRight;

        if (availableWidth <= 0) {
          throw new Error("container width is 0");
        }
        const outputScale = window.devicePixelRatio || 1;

        for (let pageNumber = 1; pageNumber <= pdf.numPages; pageNumber += 1) {
          const page = await pdf.getPage(pageNumber);
          if (cancelled) return;

          const viewport = page.getViewport({ scale: 1 });
          const scale = availableWidth / viewport.width;
          const scaledViewport = page.getViewport({ scale });
          const renderViewport = page.getViewport({
            scale: scale * outputScale,
          });

          const pageWrapper = document.createElement("div");
          pageWrapper.className = "pdf-page";
          // pdf.js viewer CSS relies on this variable for proper text-layer sizing.
          pageWrapper.style.setProperty("--scale-factor", String(scale));
          pageWrapper.style.width = `${scaledViewport.width}px`;
          pageWrapper.style.height = `${scaledViewport.height}px`;

          const canvas = document.createElement("canvas");
          canvas.className = "pdf-page-canvas";
          const context = canvas.getContext("2d");
          if (!context) {
            throw new Error(
              t("preview.errors.pdfDisplayFailed", { ns: "files" }),
            );
          }

          canvas.width = Math.ceil(renderViewport.width);
          canvas.height = Math.ceil(renderViewport.height);
          canvas.style.width = `${scaledViewport.width}px`;
          canvas.style.height = `${scaledViewport.height}px`;

          const textLayerDiv = document.createElement("div");
          textLayerDiv.className = "textLayer";
          textLayerDiv.style.width = `${scaledViewport.width}px`;
          textLayerDiv.style.height = `${scaledViewport.height}px`;

          pageWrapper.appendChild(canvas);
          pageWrapper.appendChild(textLayerDiv);
          container.appendChild(pageWrapper);

          const renderTask = page.render({
            canvasContext: context,
            viewport: renderViewport,
          });
          await renderTask.promise;

          if (cancelled) return;

          // Text layer enables selection/copy.
          const textContent = await page.getTextContent();
          if (cancelled) return;
          const textLayer = new TextLayer({
            textContentSource: textContent,
            container: textLayerDiv,
            viewport: scaledViewport,
          });
          await textLayer.render();

          if (cancelled) return;
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
  }, [isTooLarge, pdfBlob, shouldUsePdfJs, t]);

  const handleLoad = () => {
    setLoading(false);
  };

  const handleError = () => {
    setLoading(false);
    setError(null);
    setForcePdfJs(true);
  };

  return (
    <Box
      sx={{
        width: "100%",
        height: "100%",
        display: "flex",
        flexDirection: "column",
        position: "relative",
        pt: 1,
      }}
    >
      {isTooLarge && (
        <Box
          sx={{
            width: "100%",
            height: "100%",
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            justifyContent: "center",
            gap: 2,
            p: 3,
          }}
        >
          <Typography variant="body1" color="text.secondary" align="center">
            {t("preview.errors.pdfTooLarge", {
              ns: "files",
              size: formatBytes(fileSizeBytes ?? 0),
              maxSize: `${Math.round(maxMB)} MB`,
            })}
          </Typography>
        </Box>
      )}

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
      {!shouldUsePdfJs && blobUrl && (
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
      {shouldUsePdfJs && !error && (
        <Box
          ref={renderContainerRef}
          sx={{
            width: "100%",
            height: "100%",
            overflowY: "auto",
            overflowX: "hidden",
            display: "block",
            px: 1,
            "& .pdf-page": {
              position: "relative",
              mx: "auto",
              mb: 1.5,
            },
            "& .pdf-page-canvas": {
              display: "block",
              borderRadius: 0.4,
              pointerEvents: "none",
            },
            "& .textLayer": {
              position: "absolute",
              inset: 0,
              overflow: "hidden",
              opacity: 1,
              color: "transparent",
              userSelect: "text",
            },
            "& .textLayer span": {
              position: "absolute",
              whiteSpace: "pre",
              cursor: "text",
              transformOrigin: "0% 0%",
            },
          }}
        />
      )}
    </Box>
  );
};
