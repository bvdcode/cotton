import React from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Typography,
} from "@mui/material";
import {
  Description,
  Image,
  InsertDriveFile,
  Movie,
  PictureAsPdf,
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

  const filenameStarMatch = contentDisposition.match(
    /filename\*=([^']*)''([^;]+)/i,
  );
  if (filenameStarMatch && filenameStarMatch[2]) {
    try {
      return decodeURIComponent(filenameStarMatch[2]);
    } catch {
      return filenameStarMatch[2];
    }
  }

  const filenameMatch = contentDisposition.match(/filename="?([^";]+)"?/i);
  if (filenameMatch && filenameMatch[1]) {
    return filenameMatch[1];
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
        const parsedName = tryParseFileName(cd);
        setFileName(parsedName);

        const parsedLength = cl ? Number.parseInt(cl, 10) : Number.NaN;
        setContentLength(Number.isFinite(parsedLength) ? parsedLength : null);

        const kind =
          guessViewerKind(ct) !== "unknown"
            ? guessViewerKind(ct)
            : guessViewerKindFromName(parsedName);

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
  }, [inlineUrl, t, token]);

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

  return (
    <Box
      width="100%"
      sx={{
        display: "flex",
        flexDirection: "column",
        flex: 1,
        minHeight: 0,
        minWidth: 0,
        px: { xs: 1, sm: 2 },
        py: { xs: 1, sm: 2 },
      }}
    >
      <Box
        sx={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          gap: 1,
          mb: 1,
          minWidth: 0,
        }}
      >
        <Typography variant="h6" noWrap sx={{ minWidth: 0 }}>
          {title}
        </Typography>
        {downloadUrl && (
          <Button
            href={downloadUrl}
            target="_blank"
            rel="noreferrer"
            variant="outlined"
          >
            {t("actions.download", { ns: "common" })}
          </Button>
        )}
      </Box>

      {loading && (
        <Box
          sx={{
            flex: 1,
            minHeight: 0,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            gap: 2,
          }}
        >
          <CircularProgress size={20} />
          <Typography color="text.secondary">
            {t("loading", { ns: "share" })}
          </Typography>
        </Box>
      )}

      {!loading && error && <Alert severity="error">{error}</Alert>}

      {!loading && !error && previewUrl && (
        <Box
          sx={{
            flex: 1,
            minHeight: 0,
            border: 1,
            borderColor: "divider",
            borderRadius: 1,
            overflow: "hidden",
          }}
        >
          {viewerKind === "image" && !previewFailed && (
            <Box
              component="img"
              src={previewUrl}
              alt={fileName ?? ""}
              onError={() => setPreviewFailed(true)}
              sx={{
                width: "100%",
                height: "100%",
                objectFit: "contain",
                display: "block",
              }}
            />
          )}

          {viewerKind === "video" && !previewFailed && (
            <Box
              component="video"
              src={previewUrl}
              controls
              onError={() => setPreviewFailed(true)}
              sx={{ width: "100%", height: "100%", display: "block" }}
            />
          )}

          {viewerKind === "pdf" && !previewFailed && pdfBlobUrl && (
            <Box
              component="iframe"
              src={pdfBlobUrl}
              title={title}
              sx={{ width: "100%", height: "100%", border: "none" }}
            />
          )}

          {viewerKind === "text" && !previewFailed && (
            <Box
              sx={{
                width: "100%",
                height: "100%",
                overflow: "auto",
                p: 2,
              }}
            >
              <Typography component="pre" sx={{ m: 0, whiteSpace: "pre-wrap" }}>
                {textContent ?? ""}
              </Typography>
            </Box>
          )}

          {(viewerKind === "unknown" ||
            previewFailed ||
            (viewerKind === "pdf" && !pdfBlobUrl)) && (
            <Box
              sx={{
                width: "100%",
                height: "100%",
                display: "flex",
                flexDirection: "column",
                alignItems: "center",
                justifyContent: "center",
                p: 2,
                gap: 1,
              }}
            >
              <Box
                sx={{
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  "& > svg": { width: 56, height: 56 },
                  color: "text.secondary",
                }}
              >
                {getFallbackIcon(fallbackKind)}
              </Box>

              {contentLength !== null && (
                <Typography color="text.secondary" variant="body2">
                  {formatBytes(contentLength)}
                </Typography>
              )}

              {contentType && (
                <Typography color="text.secondary" variant="caption" noWrap>
                  {contentType}
                </Typography>
              )}

              <Typography color="text.secondary" sx={{ mt: 1 }}>
                {t("unsupported", { ns: "share" })}
              </Typography>
            </Box>
          )}
        </Box>
      )}
    </Box>
  );
};
