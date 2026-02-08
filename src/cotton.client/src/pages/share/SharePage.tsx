import React from "react";
import {
  Alert,
  Box,
  Button,
  Container,
  CircularProgress,
  Snackbar,
  Typography,
} from "@mui/material";
import {
  Description,
  Image,
  InsertDriveFile,
  Movie,
  PictureAsPdf,
  Download,
  Share,
} from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { useParams } from "react-router-dom";
import { shareLinks } from "../../shared/utils/shareLinks";
import { formatBytes } from "../files/utils/formatBytes";

type ViewerKind = "image" | "video" | "pdf" | "text" | "unknown";

function guessViewerKind(contentType: string | null): ViewerKind {
  if (!contentType) return "unknown";
  const ct = contentType.toLowerCase();
  if (ct.startsWith("image/")) return "image";
  if (ct.startsWith("video/")) return "video";
  if (ct.includes("pdf")) return "pdf";
  if (ct.startsWith("text/")) return "text";
  if (ct.includes("json") || ct.includes("xml")) return "text";
  return "unknown";
}

function guessViewerKindFromName(fileName: string | null): ViewerKind {
  if (!fileName) return "unknown";
  const lower = fileName.toLowerCase();

  if (lower.endsWith(".pdf")) return "pdf";
  if (
    lower.endsWith(".png") ||
    lower.endsWith(".jpg") ||
    lower.endsWith(".jpeg") ||
    lower.endsWith(".gif") ||
    lower.endsWith(".webp") ||
    lower.endsWith(".bmp") ||
    lower.endsWith(".svg")
  ) {
    return "image";
  }
  if (
    lower.endsWith(".mp4") ||
    lower.endsWith(".webm") ||
    lower.endsWith(".mov") ||
    lower.endsWith(".mkv")
  ) {
    return "video";
  }
  if (
    lower.endsWith(".txt") ||
    lower.endsWith(".log") ||
    lower.endsWith(".md") ||
    lower.endsWith(".json") ||
    lower.endsWith(".xml")
  ) {
    return "text";
  }
  return "unknown";
}

function getFallbackIcon(kind: ViewerKind) {
  switch (kind) {
    case "pdf":
      return <PictureAsPdf />;
    case "image":
      return <Image />;
    case "video":
      return <Movie />;
    case "text":
      return <Description />;
    default:
      return <InsertDriveFile />;
  }
}

function tryParseFileName(contentDisposition: string | null): string | null {
  if (!contentDisposition) return null;

  // RFC 5987 / RFC 6266: prefer `filename*` over `filename`.
  // Example: inline; filename*=UTF-8''20250903_130511.heic; filename=20250903_130511.heic
  const filenameStarMatch = contentDisposition.match(
    /filename\*\s*=\s*([^']+)''([^;]+)/i,
  );
  if (filenameStarMatch?.[2]) {
    const value = filenameStarMatch[2].trim();
    try {
      return decodeURIComponent(value);
    } catch {
      return value;
    }
  }

  const filenameMatch = contentDisposition.match(/filename\s*=\s*([^;]+)/i);
  if (filenameMatch?.[1]) {
    const value = filenameMatch[1].trim();
    if (
      (value.startsWith('"') && value.endsWith('"')) ||
      (value.startsWith("'") && value.endsWith("'"))
    ) {
      return value.slice(1, -1);
    }
    return value;
  }

  return null;
}

export const SharePage: React.FC = () => {
  const { t } = useTranslation(["share", "common"]);
  const params = useParams<{ token?: string }>();
  const token = params.token ?? null;

  const inlineUrl = React.useMemo(() => {
    if (!token) return null;
    return shareLinks.buildTokenDownloadUrl(token, "inline");
  }, [token]);

  const downloadUrl = React.useMemo(() => {
    if (!token) return null;
    return shareLinks.buildTokenDownloadUrl(token, "download");
  }, [token]);

  const shareUrl = React.useMemo(() => {
    if (!token) return null;
    return shareLinks.buildShareUrl(token);
  }, [token]);

  const [loading, setLoading] = React.useState<boolean>(true);
  const [error, setError] = React.useState<string | null>(null);

  const [fileName, setFileName] = React.useState<string | null>(null);
  const [contentType, setContentType] = React.useState<string | null>(null);
  const [contentLength, setContentLength] = React.useState<number | null>(null);
  const [textContent, setTextContent] = React.useState<string | null>(null);
  const [resolvedDownloadUrl, setResolvedDownloadUrl] = React.useState<
    string | null
  >(null);
  const [pdfBlobUrl, setPdfBlobUrl] = React.useState<string | null>(null);
  const [previewFailed, setPreviewFailed] = React.useState<boolean>(false);

  const [shareToast, setShareToast] = React.useState<{
    open: boolean;
    message: string;
  }>({ open: false, message: "" });

  const viewerKind = React.useMemo<ViewerKind>(() => {
    const byType = guessViewerKind(contentType);
    if (byType !== "unknown") return byType;
    return guessViewerKindFromName(fileName);
  }, [contentType, fileName]);

  React.useEffect(() => {
    if (!token || !inlineUrl) {
      setLoading(false);
      setError(t("errors.invalidLink", { ns: "share" }));
      return;
    }

    let cancelled = false;

    const load = async () => {
      setLoading(true);
      setError(null);
      setFileName(null);
      setContentType(null);
      setContentLength(null);
      setTextContent(null);
      setResolvedDownloadUrl(null);
      setPdfBlobUrl(null);
      setPreviewFailed(false);

      try {
        let response: Response;
        try {
          response = await fetch(inlineUrl, { method: "HEAD" });
          if (!response.ok) {
            response = await fetch(inlineUrl, { method: "GET" });
          }
        } catch {
          response = await fetch(inlineUrl, { method: "GET" });
        }

        if (cancelled) return;

        if (!response.ok) {
          setError(t("errors.notFound", { ns: "share" }));
          setLoading(false);
          return;
        }

        setResolvedDownloadUrl(inlineUrl);

        const ct = response.headers.get("content-type");
        const cd = response.headers.get("content-disposition");
        const cl = response.headers.get("content-length");

        setContentType(ct);
        let resolvedName: string | null = tryParseFileName(cd);
        if (!resolvedName && downloadUrl) {
          try {
            const nameResp = await fetch(downloadUrl, { method: "HEAD" });
            if (nameResp.ok) {
              resolvedName = tryParseFileName(
                nameResp.headers.get("content-disposition"),
              );
            }
          } catch {
            // ignore
          }
        }
        if (cancelled) return;
        setFileName(resolvedName);

        const parsedLength = cl ? Number.parseInt(cl, 10) : Number.NaN;
        setContentLength(Number.isFinite(parsedLength) ? parsedLength : null);

        const byType = guessViewerKind(ct);
        const kind = byType !== "unknown" ? byType : guessViewerKindFromName(resolvedName);

        if (kind === "text") {
          const textResp = await fetch(inlineUrl, { method: "GET" });
          if (!textResp.ok) {
            throw new Error("text download failed");
          }
          const text = await textResp.text();
          if (cancelled) return;
          setTextContent(text);
        }

        setLoading(false);
      } catch {
        if (cancelled) return;
        setError(t("errors.loadFailed", { ns: "share" }));
        setLoading(false);
      }
    };

    void load();

    return () => {
      cancelled = true;
    };
  }, [downloadUrl, inlineUrl, t, token]);

  React.useEffect(() => {
    if (!resolvedDownloadUrl) return;
    if (viewerKind !== "pdf") return;

    let cancelled = false;
    let nextUrl: string | null = null;

    const loadPdfBlob = async () => {
      try {
        const resp = await fetch(resolvedDownloadUrl);
        if (!resp.ok) {
          throw new Error("download failed");
        }
        const blob = await resp.blob();
        if (cancelled) return;
        nextUrl = URL.createObjectURL(blob);
        setPdfBlobUrl(nextUrl);
      } catch {
        if (cancelled) return;
        setPreviewFailed(true);
      }
    };

    void loadPdfBlob();

    return () => {
      cancelled = true;
      if (nextUrl) {
        URL.revokeObjectURL(nextUrl);
      }
    };
  }, [resolvedDownloadUrl, viewerKind]);

  const previewUrl = resolvedDownloadUrl;
  const fallbackKind: ViewerKind =
    viewerKind === "unknown" ? "unknown" : viewerKind;
  const title = fileName ?? t("title", { ns: "share" });

  const previewSupported =
    Boolean(previewUrl) && viewerKind !== "unknown" && !previewFailed;

  const isPdfPreviewLoading = previewSupported && viewerKind === "pdf" && !pdfBlobUrl;
  const isTextPreviewLoading =
    previewSupported && viewerKind === "text" && textContent === null;

  const handleDownload = () => {
    if (!downloadUrl) return;
    const link = document.createElement("a");
    link.href = downloadUrl;
    link.download = fileName ?? "file";
    link.target = "_blank";
    link.rel = "noopener noreferrer";
    link.style.display = "none";
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  const handleShareLink = React.useCallback(async () => {
    const url = shareUrl ?? window.location.href;

    if (typeof navigator !== "undefined" && typeof navigator.share === "function") {
      try {
        await navigator.share({ title: fileName ?? undefined, url });
        setShareToast({
          open: true,
          message: t("toasts.shared", { ns: "share" }),
        });
        return;
      } catch (e) {
        if (e instanceof Error && e.name === "AbortError") {
          return;
        }
      }
    }

    try {
      await navigator.clipboard.writeText(url);
      setShareToast({
        open: true,
        message: t("toasts.copied", { ns: "share" }),
      });
    } catch {
      setShareToast({
        open: true,
        message: t("errors.copyLink", { ns: "share" }),
      });
    }
  }, [fileName, shareUrl, t]);

  return (
    <Box
      width="100%"
      height="100%"
      alignSelf="stretch"
      display="flex"
      flexDirection="column"
      flex={1}
      minHeight={0}
      minWidth={0}
    >
      <Snackbar
        open={shareToast.open}
        autoHideDuration={2500}
        onClose={() => setShareToast((prev) => ({ ...prev, open: false }))}
        message={shareToast.message}
      />

      {loading && (
        <Box
          flex={1}
          minHeight={0}
          display="flex"
          alignItems="center"
          justifyContent="center"
          gap={2}
        >
          <CircularProgress size={20} />
          <Typography color="text.secondary">
            {t("loading", { ns: "share" })}
          </Typography>
        </Box>
      )}

      {!loading && error && (
        <Box
          flex={1}
          minHeight={0}
          display="flex"
          alignItems="center"
          justifyContent="center"
          p={2}
        >
          <Alert severity="error">{error}</Alert>
        </Box>
      )}

      {!loading && !error && previewUrl && (
        <>
          {previewSupported && (
            <Box
              position="sticky"
              top={0}
              zIndex={1}
              bgcolor="background.default"
              display="flex"
              alignItems="center"
              justifyContent="space-between"
              gap={2}
              px={{ xs: 2, sm: 3 }}
              py={0.75}
              minWidth={0}
              borderBottom={1}
              borderColor="divider"
              sx={{ minHeight: 48 }}
            >
              <Box
                display="flex"
                alignItems="center"
                gap={1}
                minWidth={0}
                flex={1}
                overflow="hidden"
              >
                <Typography variant="subtitle1" noWrap sx={{ minWidth: 0 }}>
                  {fileName ?? title}
                  {contentLength !== null && (
                    <Box component="span" sx={{ color: "text.secondary", ml: 1 }}>
                      • {formatBytes(contentLength)}
                    </Box>
                  )}
                </Typography>
              </Box>

              <Box display="flex" alignItems="center" gap={1} flexShrink={0}>
                <Button
                  onClick={handleShareLink}
                  variant="outlined"
                  startIcon={<Share />}
                  size="small"
                >
                  {t("actions.share", { ns: "common" })}
                </Button>
                {downloadUrl && (
                  <Button
                    onClick={handleDownload}
                    variant="contained"
                    startIcon={<Download />}
                    size="small"
                  >
                    {t("actions.download", { ns: "common" })}
                  </Button>
                )}
              </Box>
            </Box>
          )}

          <Box
            flex={1}
            minHeight={0}
            display="flex"
            alignItems="center"
            justifyContent="center"
            overflow="hidden"
          >
            {(isPdfPreviewLoading || isTextPreviewLoading) && (
              <Box
                display="flex"
                alignItems="center"
                justifyContent="center"
                gap={2}
              >
                <CircularProgress size={20} />
                <Typography color="text.secondary">
                  {t("loading", { ns: "share" })}
                </Typography>
              </Box>
            )}

            {viewerKind === "image" && !previewFailed && (
              <Container
                maxWidth="lg"
                disableGutters
                sx={{
                  height: "100%",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  px: { xs: 2, sm: 3 },
                }}
              >
                <Box
                  component="img"
                  src={previewUrl}
                  alt={fileName ?? ""}
                  onError={() => setPreviewFailed(true)}
                  sx={{
                    width: "100%",
                    maxHeight: "100%",
                    objectFit: "contain",
                    display: "block",
                  }}
                />
              </Container>
            )}

            {viewerKind === "video" && !previewFailed && (
              <Container
                maxWidth="lg"
                disableGutters
                sx={{
                  height: "100%",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  px: { xs: 2, sm: 3 },
                }}
              >
                <Box
                  component="video"
                  src={previewUrl}
                  controls
                  onError={() => setPreviewFailed(true)}
                  sx={{ width: "100%", maxHeight: "100%", display: "block" }}
                />
              </Container>
            )}

            {viewerKind === "pdf" && !previewFailed && pdfBlobUrl && (
              <Box
                component="iframe"
                src={pdfBlobUrl}
                title={title}
                sx={{
                  width: "100%",
                  height: "100%",
                  border: "none",
                  maxWidth: "100%",
                }}
              />
            )}

            {viewerKind === "text" && !previewFailed && textContent !== null && (
              <Box
                width="100%"
                height="100%"
                overflow="auto"
                display="flex"
                justifyContent="center"
              >
                <Container maxWidth="md" sx={{ py: { xs: 2, sm: 3 } }}>
                  <Typography component="pre" sx={{ m: 0, whiteSpace: "pre-wrap" }}>
                    {textContent}
                  </Typography>
                </Container>
              </Box>
            )}

            {(viewerKind === "unknown" || previewFailed) && (
              <Box
                display="flex"
                flexDirection="column"
                alignItems="center"
                justifyContent="center"
                p={2}
                gap={1}
              >
                <Box
                  display="flex"
                  alignItems="center"
                  justifyContent="center"
                  sx={{
                    "& > svg": { width: 80, height: 80 },
                    color: "text.secondary",
                  }}
                >
                  {getFallbackIcon(fallbackKind)}
                </Box>

                {fileName && (
                  <Typography
                    color="text.primary"
                    variant="h6"
                    align="center"
                    sx={{ mt: 2 }}
                  >
                    {fileName}
                  </Typography>
                )}

                {contentLength !== null && (
                  <Typography color="text.secondary" variant="body2">
                    {formatBytes(contentLength)}
                  </Typography>
                )}

                {contentType && (
                  <Typography color="text.secondary" variant="caption">
                    {contentType}
                  </Typography>
                )}

                {previewFailed && (
                  <Typography color="text.secondary" sx={{ mt: 1 }}>
                    {t("unsupported", { ns: "share" })}
                  </Typography>
                )}
              </Box>
            )}
          </Box>

          {downloadUrl && !previewSupported && (
            <Box
              display="flex"
              justifyContent="center"
              alignItems="center"
              p={3}
            >
              <Button
                onClick={handleDownload}
                variant="contained"
                size="large"
                startIcon={<Download />}
                sx={{ minWidth: 200 }}
              >
                {t("actions.download", { ns: "common" })}
              </Button>
            </Box>
          )}
        </>
      )}
    </Box>
  );
};
