import { Box, CircularProgress, Typography } from "@mui/material";
import { useState, useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { previewConfig } from "../../config/previewConfig";
import { formatBytes } from "../../utils/formatBytes";
import "pdfjs-dist/web/pdf_viewer.css";
import {
  getDocument,
  GlobalWorkerOptions,
} from "pdfjs-dist/legacy/build/pdf.mjs";
import { renderPdfPages } from "./renderPdfPages";
import pdfWorker from "pdfjs-dist/legacy/build/pdf.worker.min.mjs?url";

import {
  type PdfPreviewSource,
  getCachedPdfUrl,
  createPdfPreviewUrl,
  resolvePdfPreviewUrl,
} from "./pdfPreviewSource";

interface PdfPreviewProps {
  source: PdfPreviewSource;
  fileName: string;
  fileSizeBytes?: number | null;
}

export const PdfPreview = ({
  source,
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

  const cachedBlobUrl = getCachedPdfUrl(source);
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
        setLoadingStage("link");
        const previewUrl = await resolvePdfPreviewUrl(source);

        if (cancelled) {
          return;
        }

        // Step 2: Fetch as blob to avoid SPA routing intercept.
        setLoadingStage("download");
        const response = await fetch(previewUrl);

        if (cancelled) {
          return;
        }

        if (!response.ok) {
          throw new Error(t("preview.errors.downloadFailed", { ns: "files" }));
        }

        // Step 3: Create blob URL
        // Blob URLs are not intercepted by React Router and work in iframe
        const blob = await response.blob();
        if (cancelled) {
          return;
        }
        const url = createPdfPreviewUrl(source, blob);
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
  }, [blobUrl, isTooLarge, pdfBlob, shouldUsePdfJs, source, t]);

  useEffect(() => {
    if (isTooLarge) return;
    if (!shouldUsePdfJs || !pdfBlob) return;

    let cancelled = false;
    let loadingTask: ReturnType<typeof getDocument> | null = null;

    const renderPdf = async () => {
      setRendering(true);
      setLoadingStage("render");
      setError(null);

      try {
        GlobalWorkerOptions.workerSrc = pdfWorker;
        const data = await pdfBlob.arrayBuffer();
        if (cancelled) {
          return;
        }
        loadingTask = getDocument({ data });
        const pdf = await loadingTask.promise;

        if (cancelled) {
          return;
        }

        const container = renderContainerRef.current;
        if (!container) {
          setRendering(false);
          return;
        }

        await renderPdfPages(
          pdf,
          container,
          () => cancelled,
          t("preview.errors.pdfDisplayFailed", { ns: "files" }),
        );
        if (cancelled) {
          return;
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
      void loadingTask?.destroy();
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
          <Typography
            variant="body1"
            align="center"
            sx={{
              color: "text.secondary",
            }}
          >
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
          <Typography
            variant="body2"
            sx={{
              color: "text.secondary",
            }}
          >
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
          <Typography
            variant="body2"
            sx={{
              color: "text.secondary",
              mt: 1,
            }}
          >
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
