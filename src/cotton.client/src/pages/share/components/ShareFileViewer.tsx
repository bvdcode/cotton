import * as React from "react";
import { Box, Container, Typography } from "@mui/material";
import {
  Description,
  Image as ImageIcon,
  InsertDriveFile,
  Movie,
  PictureAsPdf,
} from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { getFileTypeInfo, type FileType } from "../../files/utils/fileTypes";
import { formatBytes } from "../../../shared/utils/formatBytes";
import { MediaLightbox, type MediaItem, PdfPreview } from "../../files/components";
import { CodeEditor } from "../../files/components/preview/editors/CodeEditor";
import { MarkdownEditor } from "../../files/components/preview/editors/MarkdownEditor";
import { PlainTextEditor } from "../../files/components/preview/editors/PlainTextEditor";
import { detectMonacoLanguageFromFileName } from "../../../shared/utils/languageDetection";

function detectMonacoLanguageFromContentType(
  contentType: string | null,
): string | null {
  if (!contentType) return null;
  const normalized = contentType.toLowerCase();

  if (normalized.includes("json")) return "json";
  if (normalized.includes("xml")) return "xml";
  if (normalized.includes("yaml") || normalized.includes("yml")) return "yaml";

  return null;
}

interface ShareFileViewerProps {
  token: string;
  title: string;
  inlineUrl: string;
  downloadUrl: string | null;
  fileName: string | null;
  contentType: string | null;
  contentLength: number | null;
  textContent: string | null;
}

function getFallbackIcon(fileType: FileType) {
  switch (fileType) {
    case "pdf":
      return <PictureAsPdf />;
    case "image":
      return <ImageIcon />;
    case "video":
      return <Movie />;
    case "text":
      return <Description />;
    default:
      return <InsertDriveFile />;
  }
}

interface ShareMediaViewerProps {
  token: string;
  title: string;
  inlineUrl: string;
  downloadUrl: string | null;
  fileName: string | null;
  contentType: string | null;
  contentLength: number | null;
}

const ShareMediaViewer: React.FC<ShareMediaViewerProps> = ({
  token,
  title,
  inlineUrl,
  downloadUrl,
  fileName,
  contentType,
  contentLength,
}) => {
  const [lightboxOpen, setLightboxOpen] = React.useState<boolean>(false);

  const fileTypeInfo = React.useMemo(() => {
    const name = fileName ?? "";
    return getFileTypeInfo(name, contentType);
  }, [contentType, fileName]);

  React.useEffect(() => {
    if (fileTypeInfo.type === "image" || fileTypeInfo.type === "video") {
      setLightboxOpen(true);
      return;
    }
    setLightboxOpen(false);
  }, [fileTypeInfo.type]);

  if (fileTypeInfo.type !== "image" && fileTypeInfo.type !== "video") {
    return null;
  }

  const item: MediaItem = {
    id: token,
    kind: fileTypeInfo.type,
    name: fileName ?? title,
    previewUrl: inlineUrl,
    mimeType: contentType ?? "application/octet-stream",
    sizeBytes: contentLength ?? undefined,
  };

  return (
    <Box width="100%" height="100%">
      <MediaLightbox
        items={[item]}
        open={lightboxOpen}
        initialIndex={0}
        onClose={() => setLightboxOpen(false)}
        getSignedMediaUrl={async () => inlineUrl}
        getDownloadUrl={downloadUrl ? async () => downloadUrl : undefined}
      />

      {!lightboxOpen && (
        <Box
          width="100%"
          height="100%"
          display="flex"
          alignItems="center"
          justifyContent="center"
        >
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
            {fileTypeInfo.type === "image" ? (
              <Box
                component="img"
                src={inlineUrl}
                alt={fileName ?? ""}
                onClick={() => setLightboxOpen(true)}
                sx={{
                  width: "100%",
                  maxHeight: "100%",
                  objectFit: "contain",
                  display: "block",
                  cursor: "pointer",
                }}
              />
            ) : (
              <Box
                component="video"
                src={inlineUrl}
                controls
                onPlay={() => setLightboxOpen(true)}
                sx={{ width: "100%", maxHeight: "100%", display: "block" }}
              />
            )}
          </Container>
        </Box>
      )}
    </Box>
  );
};

interface ShareTextViewerProps {
  title: string;
  fileName: string | null;
  contentType: string | null;
  textContent: string;
}

const ShareTextViewer: React.FC<ShareTextViewerProps> = ({
  title,
  fileName,
  contentType,
  textContent,
}) => {
  const handleReadOnlyChange = React.useCallback((_nextValue: string) => {
    void _nextValue;
  }, []);

  const resolvedFileName = fileName ?? title;
  const lowerName = resolvedFileName.toLowerCase();
  const isMarkdown = lowerName.endsWith(".md") || lowerName.endsWith(".markdown");

  if (isMarkdown) {
    return (
      <Box width="100%" height="100%" minHeight={0} minWidth={0}>
        <MarkdownEditor
          value={textContent}
          onChange={handleReadOnlyChange}
          isEditing={false}
          fileName={resolvedFileName}
        />
      </Box>
    );
  }

  const languageOverride =
    fileName === null ? detectMonacoLanguageFromContentType(contentType) : null;
  const detectedLanguage = detectMonacoLanguageFromFileName(resolvedFileName);
  const monacoLanguage = languageOverride ?? detectedLanguage;
  const shouldUseCodeEditor = monacoLanguage !== "plaintext";

  return (
    <Box width="100%" height="100%" minHeight={0} minWidth={0}>
      {shouldUseCodeEditor ? (
        <CodeEditor
          value={textContent}
          onChange={handleReadOnlyChange}
          isEditing={false}
          fileName={resolvedFileName}
          language={languageOverride ?? undefined}
        />
      ) : (
        <PlainTextEditor
          value={textContent}
          onChange={handleReadOnlyChange}
          isEditing={false}
          fileName={resolvedFileName}
        />
      )}
    </Box>
  );
};

interface ShareUnsupportedViewerProps {
  fileType: FileType;
  fileName: string | null;
  contentType: string | null;
  contentLength: number | null;
}

const ShareUnsupportedViewer: React.FC<ShareUnsupportedViewerProps> = ({
  fileType,
  fileName,
  contentType,
  contentLength,
}) => {
  const { t } = useTranslation(["share"]);

  return (
    <Box
      width="100%"
      height="100%"
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
        {getFallbackIcon(fileType)}
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

      <Typography color="text.secondary" sx={{ mt: 1 }}>
        {t("unsupported", { ns: "share" })}
      </Typography>
    </Box>
  );
};

export const ShareFileViewer: React.FC<ShareFileViewerProps> = ({
  token,
  title,
  inlineUrl,
  downloadUrl,
  fileName,
  contentType,
  contentLength,
  textContent,
}) => {
  const fileTypeInfo = React.useMemo(() => {
    const name = fileName ?? "";
    return getFileTypeInfo(name, contentType);
  }, [contentType, fileName]);

  if (fileTypeInfo.type === "image" || fileTypeInfo.type === "video") {
    return (
      <ShareMediaViewer
        token={token}
        title={title}
        inlineUrl={inlineUrl}
        downloadUrl={downloadUrl}
        fileName={fileName}
        contentType={contentType}
        contentLength={contentLength}
      />
    );
  }

  if (fileTypeInfo.type === "pdf") {
    return (
      <Box width="100%" height="100%">
        <PdfPreview
          source={{
            kind: "url",
            cacheKey: `share:${token}`,
            getPreviewUrl: async () => inlineUrl,
          }}
          fileName={fileName ?? title}
          fileSizeBytes={contentLength}
        />
      </Box>
    );
  }

  if (fileTypeInfo.type === "text" && textContent !== null) {
    return (
      <ShareTextViewer
        title={title}
        fileName={fileName}
        contentType={contentType}
        textContent={textContent}
      />
    );
  }

  return (
    <ShareUnsupportedViewer
      fileType={fileTypeInfo.type}
      fileName={fileName}
      contentType={contentType}
      contentLength={contentLength}
    />
  );
};
