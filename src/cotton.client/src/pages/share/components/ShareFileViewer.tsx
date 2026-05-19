import * as React from "react";
import { Box, Container, Stack, Typography } from "@mui/material";
import {
  Description,
  Image as ImageIcon,
  InsertDriveFile,
  LockOutlined,
  Movie,
  PictureAsPdf,
  ViewInAr,
} from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { getFileTypeInfo, type FileType } from "@shared/utils/fileTypes";
import { formatBytes } from "../../../shared/utils/formatBytes";
import { MediaLightbox, ModelPreview, PdfPreview } from "@shared/ui/preview";
import type { MediaItem } from "@shared/types/mediaLightbox";
import {
  selectGallerySmoothTransitions,
  useUserPreferencesStore,
} from "../../../shared/store/userPreferencesStore";
import { ReadOnlyTextViewer } from "./ReadOnlyTextViewer";

interface ShareFileViewerProps {
  token: string;
  title: string;
  inlineUrl: string;
  downloadUrl: string | null;
  fileName: string | null;
  contentType: string | null;
  contentLength: number | null;
  textContent: string | null;
  encryptedContainer: boolean;
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
    case "model":
      return <ViewInAr />;
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
  const [closedLightboxKey, setClosedLightboxKey] = React.useState<string | null>(null);
  const smoothGalleryTransitions = useUserPreferencesStore(
    selectGallerySmoothTransitions,
  );

  const fileTypeInfo = React.useMemo(() => {
    const name = fileName ?? "";
    return getFileTypeInfo(name, contentType);
  }, [contentType, fileName]);

  const lightboxKey = [token, fileTypeInfo.type].join(":");
  const lightboxOpen = closedLightboxKey !== lightboxKey;
  const reopenLightbox = React.useCallback(() => {
    setClosedLightboxKey(null);
  }, []);

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
        onClose={() => setClosedLightboxKey(lightboxKey)}
        getSignedMediaUrl={async () => inlineUrl}
        getDownloadUrl={downloadUrl ? async () => downloadUrl : undefined}
        smoothTransitions={smoothGalleryTransitions}
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
                onClick={reopenLightbox}
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
                onPlay={reopenLightbox}
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
  return (
    <ReadOnlyTextViewer
      title={title}
      fileName={fileName}
      contentType={contentType}
      textContent={textContent}
    />
  );
};

interface ShareEncryptedFileNoticeProps {
  fileName: string | null;
  contentLength: number | null;
}

const ShareEncryptedFileNotice: React.FC<ShareEncryptedFileNoticeProps> = ({
  fileName,
  contentLength,
}) => {
  const { t } = useTranslation(["share"]);

  return (
    <Box
      width="100%"
      height="100%"
      display="flex"
      alignItems="center"
      justifyContent="center"
      p={2}
    >
      <Stack
        alignItems="center"
        spacing={1}
        maxWidth={520}
        textAlign="center"
      >
        <Box
          display="flex"
          alignItems="center"
          justifyContent="center"
          sx={{
            color: "warning.main",
            "& > svg": { width: 64, height: 64 },
          }}
        >
          <LockOutlined />
        </Box>

        <Typography variant="h6" color="text.primary">
          {t("encryptedFile.title", { ns: "share" })}
        </Typography>

        <Typography color="text.secondary">
          {t("encryptedFile.description", { ns: "share" })}
        </Typography>

        {fileName && (
          <Typography
            color="text.primary"
            variant="body2"
            sx={{ mt: 1, maxWidth: "100%", overflowWrap: "anywhere" }}
          >
            {fileName}
          </Typography>
        )}

        {contentLength !== null && (
          <Typography color="text.secondary" variant="caption">
            {formatBytes(contentLength)}
          </Typography>
        )}
      </Stack>
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
  encryptedContainer,
}) => {
  const fileTypeInfo = React.useMemo(() => {
    const name = fileName ?? "";
    return getFileTypeInfo(name, contentType);
  }, [contentType, fileName]);

  if (encryptedContainer) {
    return (
      <ShareEncryptedFileNotice
        fileName={fileName}
        contentLength={contentLength}
      />
    );
  }

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

  if (fileTypeInfo.type === "model") {
    return (
      <Box width="100%" height="100%">
        <ModelPreview
          source={{
            kind: "url",
            url: inlineUrl,
          }}
          fileName={fileName ?? title}
          contentType={contentType}
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
