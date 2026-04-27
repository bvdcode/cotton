import * as React from "react";
import {
  Box,
  CircularProgress,
  Collapse,
  IconButton,
  Stack,
  Tooltip,
  Typography,
  useTheme,
} from "@mui/material";
import {
  AutoFixHigh,
  ColorLens,
  FormatColorReset,
  VerticalAlignBottom,
} from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { PreviewModal, PdfPreview, ModelPreview } from "../../files/components/preview";
import type { FileType } from "../../files/utils/fileTypes";
import { sharedFoldersApi } from "../../../shared/api/sharedFoldersApi";
import { previewConfig } from "../../../shared/config/previewConfig";
import { ReadOnlyTextViewer } from "./ReadOnlyTextViewer";

interface SharedFilePreviewModalProps {
  open: boolean;
  token: string;
  fileId: string | null;
  fileName: string | null;
  fileType: FileType | null;
  fileSizeBytes: number | null;
  contentType: string | null;
  onClose: () => void;
}

export const SharedFilePreviewModal: React.FC<SharedFilePreviewModalProps> = ({
  open,
  token,
  fileId,
  fileName,
  fileType,
  fileSizeBytes,
  contentType,
  onClose,
}) => {
  const { t } = useTranslation(["files", "share", "common"]);
  const theme = useTheme();
  const isModel = fileType === "model";

  const [textContent, setTextContent] = React.useState<string | null>(null);
  const [loadingText, setLoadingText] = React.useState<boolean>(false);
  const [textError, setTextError] = React.useState<string | null>(null);
  const [isPaletteOpen, setIsPaletteOpen] = React.useState<boolean>(false);
  const [materialColor, setMaterialColor] = React.useState<string | null>(null);
  const [autoAlignToken, setAutoAlignToken] = React.useState<number>(0);
  const [autoOrientToken, setAutoOrientToken] = React.useState<number>(0);

  const paletteColors = React.useMemo<string[]>(
    () => [
      theme.palette.grey[400],
      theme.palette.primary.main,
      theme.palette.info.main,
      theme.palette.success.main,
      theme.palette.warning.main,
      theme.palette.error.main,
    ],
    [theme],
  );

  const modelSource = React.useMemo(() => {
    if (!fileId) {
      return null;
    }

    return {
      kind: "url" as const,
      url: sharedFoldersApi.buildFileContentUrl(token, fileId, "inline"),
    };
  }, [fileId, token]);

  React.useEffect(() => {
    if (!open || !isModel) {
      setIsPaletteOpen(false);
      setMaterialColor(null);
      return;
    }

    setIsPaletteOpen(false);
    setMaterialColor(null);
  }, [fileId, isModel, open]);

  React.useEffect(() => {
    if (!open || fileType !== "text" || !fileId || !fileName) {
      setTextContent(null);
      setTextError(null);
      setLoadingText(false);
      return;
    }

    if (
      typeof fileSizeBytes === "number" &&
      fileSizeBytes > previewConfig.MAX_TEXT_PREVIEW_SIZE_BYTES
    ) {
      const sizeMB = fileSizeBytes / 1024 / 1024;
      const maxKB = previewConfig.MAX_TEXT_PREVIEW_SIZE_BYTES / 1024;
      setTextError(
        t("preview.errors.fileTooLarge", {
          ns: "files",
          size: sizeMB >= 1 ? `${Math.round(sizeMB)} MB` : `${Math.round(fileSizeBytes / 1024)} KB`,
          maxSize: `${Math.round(maxKB)} KB`,
        }),
      );
      setTextContent(null);
      setLoadingText(false);
      return;
    }

    let cancelled = false;

    setLoadingText(true);
    setTextError(null);
    setTextContent(null);

    void (async () => {
      try {
        const inlineUrl = sharedFoldersApi.buildFileContentUrl(
          token,
          fileId,
          "inline",
        );
        const response = await fetch(inlineUrl);

        if (cancelled) return;

        if (!response.ok) {
          throw new Error(t("preview.errors.loadFailed", { ns: "files", error: "" }));
        }

        const text = await response.text();
        if (!cancelled) {
          setTextContent(text);
          setLoadingText(false);
        }
      } catch (e) {
        if (!cancelled) {
          setTextError(
            e instanceof Error
              ? e.message
              : t("preview.errors.loadFailed", { ns: "files", error: "" }),
          );
          setLoadingText(false);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [contentType, fileId, fileName, fileSizeBytes, fileType, open, t, token]);

  if (!open || !fileId || !fileName) {
    return null;
  }

  if (fileType !== "pdf" && fileType !== "text" && fileType !== "model") {
    return null;
  }

  const modelHeaderActions = isModel
    ? (
      <Stack direction="row" spacing={0.5} alignItems="center">
        <Tooltip title={t("preview.model.actions.autoOrient", { ns: "files" })}>
          <IconButton
            size="small"
            onClick={() => setAutoOrientToken((value) => value + 1)}
          >
            <AutoFixHigh fontSize="small" />
          </IconButton>
        </Tooltip>

        <Tooltip title={t("preview.model.actions.autoAlign", { ns: "files" })}>
          <IconButton
            size="small"
            onClick={() => setAutoAlignToken((value) => value + 1)}
          >
            <VerticalAlignBottom fontSize="small" />
          </IconButton>
        </Tooltip>

        <Tooltip title={t("preview.model.actions.togglePalette", { ns: "files" })}>
          <IconButton
            size="small"
            onClick={() => setIsPaletteOpen((isOpenValue) => !isOpenValue)}
          >
            <ColorLens
              fontSize="small"
              sx={{ color: materialColor ?? theme.palette.text.primary }}
            />
          </IconButton>
        </Tooltip>
      </Stack>
    )
    : undefined;

  return (
    <PreviewModal
      open={open}
      onClose={onClose}
      layout={fileType === "pdf" || isModel ? "header" : "overlay"}
      title={fileType === "pdf" || isModel ? fileName : undefined}
      forceFullScreen={isModel}
      headerActions={modelHeaderActions}
    >
      {fileType === "pdf" && (
        <PdfPreview
          source={{
            kind: "url",
            cacheKey: `shared:${token}:${fileId}`,
            getPreviewUrl: async () =>
              sharedFoldersApi.buildFileContentUrl(token, fileId, "inline"),
          }}
          fileName={fileName}
          fileSizeBytes={fileSizeBytes}
        />
      )}

      {fileType === "text" && (
        <Box
          sx={{
            height: "100%",
            minHeight: 0,
            minWidth: 0,
            display: "flex",
            flexDirection: "column",
          }}
        >
          {loadingText && (
            <Box
              sx={{
                flex: 1,
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                gap: 1,
              }}
            >
              <CircularProgress size={20} />
              <Typography color="text.secondary">
                {t("loading", { ns: "share" })}
              </Typography>
            </Box>
          )}

          {!loadingText && textError && (
            <Box
              sx={{
                flex: 1,
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                px: 2,
              }}
            >
              <Typography color="error">{textError}</Typography>
            </Box>
          )}

          {!loadingText && !textError && textContent !== null && (
            <ReadOnlyTextViewer
              title={fileName}
              fileName={fileName}
              contentType={contentType}
              textContent={textContent}
            />
          )}
        </Box>
      )}

      {isModel && modelSource && (
        <Box sx={{ display: "flex", flex: 1, flexDirection: "column", minHeight: 0 }}>
          <Box sx={{ flex: 1, minHeight: 0 }}>
            <ModelPreview
              source={modelSource}
              fileName={fileName}
              contentType={contentType}
              fileSizeBytes={fileSizeBytes}
              materialColor={materialColor}
              autoAlignToken={autoAlignToken}
              autoOrientToken={autoOrientToken}
            />
          </Box>

          <Collapse in={isPaletteOpen} timeout="auto" unmountOnExit>
            <Box
              sx={{
                alignItems: "center",
                backgroundColor: "background.paper",
                borderTop: 1,
                borderColor: "divider",
                display: "flex",
                justifyContent: "center",
                minHeight: 56,
                px: 1.5,
                py: 1,
              }}
            >
              <Stack direction="row" spacing={1}>
                <Tooltip title={t("preview.model.actions.resetColor", { ns: "files" })}>
                  <IconButton size="small" onClick={() => setMaterialColor(null)}>
                    <FormatColorReset fontSize="small" />
                  </IconButton>
                </Tooltip>

                {paletteColors.map((color) => (
                  <IconButton
                    key={color}
                    size="small"
                    onClick={() => setMaterialColor(color)}
                  >
                    <Box
                      sx={{
                        backgroundColor: color,
                        border: 1,
                        borderColor:
                          materialColor === color ? "text.primary" : "divider",
                        borderRadius: "50%",
                        height: 18,
                        width: 18,
                      }}
                    />
                  </IconButton>
                ))}
              </Stack>
            </Box>
          </Collapse>
        </Box>
      )}
    </PreviewModal>
  );
};
