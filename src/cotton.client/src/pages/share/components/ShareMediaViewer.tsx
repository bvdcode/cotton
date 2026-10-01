import * as React from "react";
import { Box, Container } from "@mui/material";
import { getFileTypeInfo } from "@shared/utils/fileTypes";
import { resolveImageDisplayUrl } from "@shared/utils/imageDisplayUrl";
import { MediaLightbox } from "@shared/ui/preview";
import type { MediaItem } from "@shared/types/mediaLightbox";
import {
  selectGallerySmoothTransitions,
  selectGalleryPreferPreview,
  useUserPreferencesStore,
} from "../../../shared/store/userPreferencesStore";

interface ShareMediaViewerProps {
  token: string;
  title: string;
  inlineUrl: string;
  downloadUrl: string | null;
  previewUrl: string | null;
  fileName: string | null;
  contentType: string | null;
  contentLength: number | null;
}

export const ShareMediaViewer: React.FC<ShareMediaViewerProps> = ({
  token,
  title,
  inlineUrl,
  downloadUrl,
  previewUrl,
  fileName,
  contentType,
  contentLength,
}) => {
  const [closedLightboxKey, setClosedLightboxKey] = React.useState<
    string | null
  >(null);
  const smoothGalleryTransitions = useUserPreferencesStore(
    selectGallerySmoothTransitions,
  );
  const preferPreview = useUserPreferencesStore(selectGalleryPreferPreview);

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
    previewUrl: previewUrl ?? "",
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
                src={resolveImageDisplayUrl(
                  inlineUrl,
                  preferPreview,
                  item.name,
                  contentType,
                )}
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
