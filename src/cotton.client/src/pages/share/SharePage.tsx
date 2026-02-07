import React from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Typography,
} from "@mui/material";
import { useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { shareLinks } from "../../shared/utils/shareLinks";

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

  const downloadUrlCandidates = React.useMemo(() => {
    if (!token) return [];
    return shareLinks.buildTokenDownloadUrlCandidates(token);
  }, [token]);

  const withInlineFlag = React.useCallback((url: string) => {
    return `${url}${url.includes("?") ? "&" : "?"}download=false`;
  }, []);

  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);
  const [fileName, setFileName] = React.useState<string | null>(null);
  const [contentType, setContentType] = React.useState<string | null>(null);
  const [textContent, setTextContent] = React.useState<string | null>(null);
  const [resolvedDownloadUrl, setResolvedDownloadUrl] = React.useState<string | null>(null);

  const viewerKind = React.useMemo(
    () => guessViewerKind(contentType),
    [contentType],
  );

  React.useEffect(() => {
    if (!token || downloadUrlCandidates.length === 0) {
      setLoading(false);
      setError(t("errors.invalidLink", { ns: "share" }));
      return;
    }

    let cancelled = false;

    const load = async () => {
      setLoading(true);
      setError(null);
      setTextContent(null);
      setResolvedDownloadUrl(null);

      try {
        let response: Response | null = null;
        let chosenDownloadUrl: string | null = null;

        for (const candidate of downloadUrlCandidates) {
          const inlineCandidate = withInlineFlag(candidate);
          try {
            response = await fetch(inlineCandidate, { method: "HEAD" });
            if (!response.ok) {
              continue;
            }
            chosenDownloadUrl = candidate;
            break;
          } catch {
            // Some backends may not support HEAD.
            try {
              response = await fetch(inlineCandidate, { method: "GET" });
              if (!response.ok) {
                continue;
              }
              chosenDownloadUrl = candidate;
              break;
            } catch {
              continue;
            }
          }
        }

        if (cancelled) return;

        if (!response || !chosenDownloadUrl) {
          setError(t("errors.notFound", { ns: "share" }));
          setLoading(false);
          return;
        }

        setResolvedDownloadUrl(chosenDownloadUrl);

        const ct = response.headers.get("content-type");
        const cd = response.headers.get("content-disposition");
        setContentType(ct);
        setFileName(tryParseFileName(cd));

        const kind = guessViewerKind(ct);
        if (kind === "text") {
          const inlineUrl = withInlineFlag(chosenDownloadUrl);
          const textResp = response.type === "opaque" ? await fetch(inlineUrl) : response;
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
  }, [token, downloadUrlCandidates, withInlineFlag, t]);

  const inlineUrl = React.useMemo(() => {
    if (!resolvedDownloadUrl) return null;
    return withInlineFlag(resolvedDownloadUrl);
  }, [resolvedDownloadUrl, withInlineFlag]);

  return (
    <Box
      width="100%"
      height="100%"
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
          gap: 1,
          mb: 1,
          minWidth: 0,
        }}
      >
        <Typography variant="subtitle1" noWrap sx={{ flex: 1, minWidth: 0 }}>
          {fileName ?? t("title", { ns: "share" })}
        </Typography>
        {resolvedDownloadUrl && (
          <Button
            variant="outlined"
            size="small"
            component="a"
            href={resolvedDownloadUrl}
            target="_blank"
            rel="noopener noreferrer"
          >
            {t("common:actions.download")}
          </Button>
        )}
      </Box>

      {loading && (
        <Box
          sx={{
            flex: 1,
            minHeight: 0,
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            justifyContent: "center",
            gap: 2,
          }}
        >
          <CircularProgress />
          <Typography color="text.secondary">
            {t("loading", { ns: "share" })}
          </Typography>
        </Box>
      )}

      {!loading && error && <Alert severity="error">{error}</Alert>}

      {!loading && !error && inlineUrl && (
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
          {viewerKind === "image" && (
            <Box
              component="img"
              src={inlineUrl}
              alt={fileName ?? ""}
              sx={{
                width: "100%",
                height: "100%",
                objectFit: "contain",
                display: "block",
              }}
            />
          )}

          {viewerKind === "video" && (
            <Box
              component="video"
              src={inlineUrl}
              controls
              sx={{ width: "100%", height: "100%", display: "block" }}
            />
          )}

          {viewerKind === "pdf" && (
            <Box
              component="iframe"
              src={inlineUrl}
              title={fileName ?? t("title", { ns: "share" })}
              sx={{ width: "100%", height: "100%", border: "none" }}
            />
          )}

          {viewerKind === "text" && (
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

          {viewerKind === "unknown" && (
            <Box
              sx={{
                width: "100%",
                height: "100%",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                p: 2,
              }}
            >
              <Typography color="text.secondary">
                {t("unsupported", { ns: "share" })}
              </Typography>
            </Box>
          )}
        </Box>
      )}
    </Box>
  );
};
