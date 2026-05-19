import * as React from "react";
import {
  Box,
  CircularProgress,
  IconButton,
  Popover,
  Stack,
  Tooltip,
  Typography,
  useTheme,
} from "@mui/material";
import {
  AutoFixHigh,
  ColorLens,
  FilterDrama,
  FormatColorReset,
  SwapVert,
  Texture,
  VerticalAlignBottom,
  WbSunny,
} from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { PreviewModal, PdfPreview, ModelPreview } from "@shared/ui/preview";
import { useModelPreviewControls } from "@shared/ui/preview/hooks/useModelPreviewControls";
import type { FileType } from "@shared/utils/fileTypes";
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

type SharedTextPreviewState = {
  key: string;
  loading: boolean;
  error: string | null;
  content: string | null;
};

const createIdleTextPreviewState = (key: string): SharedTextPreviewState => ({
  key,
  loading: false,
  error: null,
  content: null,
});

const createLoadingTextPreviewState = (key: string): SharedTextPreviewState => ({
  key,
  loading: true,
  error: null,
  content: null,
});

const buildTextPreviewKey = (args: {
  open: boolean;
  token: string;
  fileId: string | null;
  fileName: string | null;
  fileType: FileType | null;
  fileSizeBytes: number | null;
}): string => {
  if (!args.open || args.fileType !== "text" || !args.fileId || !args.fileName) {
    return "";
  }

  return [
    args.token,
    args.fileId,
    args.fileName,
    args.fileSizeBytes ?? "",
  ].join("\u0000");
};

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
  const defaultModelColor = React.useMemo<string | null>(() => {
    return theme.palette.error.main;
  }, [theme]);

  const textPreviewKey = React.useMemo(
    () =>
      buildTextPreviewKey({
        open,
        token,
        fileId,
        fileName,
        fileType,
        fileSizeBytes,
      }),
    [fileId, fileName, fileSizeBytes, fileType, open, token],
  );
  const textSizeError = React.useMemo(() => {
    if (
      !textPreviewKey ||
      typeof fileSizeBytes !== "number" ||
      fileSizeBytes <= previewConfig.MAX_TEXT_PREVIEW_SIZE_BYTES
    ) {
      return null;
    }

    const sizeMB = fileSizeBytes / 1024 / 1024;
    const maxKB = previewConfig.MAX_TEXT_PREVIEW_SIZE_BYTES / 1024;
    return t("preview.errors.fileTooLarge", {
      ns: "files",
      size:
        sizeMB >= 1
          ? Math.round(sizeMB) + " MB"
          : Math.round(fileSizeBytes / 1024) + " KB",
      maxSize: Math.round(maxKB) + " KB",
    });
  }, [fileSizeBytes, t, textPreviewKey]);
  const [textPreviewState, setTextPreviewState] =
    React.useState<SharedTextPreviewState>(() =>
      createIdleTextPreviewState(textPreviewKey),
    );
  const textPreview = !textPreviewKey
    ? createIdleTextPreviewState(textPreviewKey)
    : textSizeError
      ? {
          ...createIdleTextPreviewState(textPreviewKey),
          error: textSizeError,
        }
      : textPreviewState.key === textPreviewKey
        ? textPreviewState
        : createLoadingTextPreviewState(textPreviewKey);
  const paletteColors = React.useMemo<Array<{ id: string; color: string }>>(
    () => [
      { id: "grey-300", color: theme.palette.grey[300] },
      { id: "grey-500", color: theme.palette.grey[500] },
      { id: "grey-700", color: theme.palette.grey[700] },

      { id: "primary-light", color: theme.palette.primary.light },
      { id: "primary-main", color: theme.palette.primary.main },
      { id: "primary-dark", color: theme.palette.primary.dark },

      { id: "secondary-light", color: theme.palette.secondary.light },
      { id: "secondary-main", color: theme.palette.secondary.main },
      { id: "secondary-dark", color: theme.palette.secondary.dark },

      { id: "info-light", color: theme.palette.info.light },
      { id: "info-main", color: theme.palette.info.main },
      { id: "info-dark", color: theme.palette.info.dark },

      { id: "success-light", color: theme.palette.success.light },
      { id: "success-main", color: theme.palette.success.main },
      { id: "success-dark", color: theme.palette.success.dark },

      { id: "warning-light", color: theme.palette.warning.light },
      { id: "warning-main", color: theme.palette.warning.main },
      { id: "warning-dark", color: theme.palette.warning.dark },

      { id: "error-light", color: theme.palette.error.light },
      { id: "error-main", color: theme.palette.error.main },
      { id: "error-dark", color: theme.palette.error.dark },
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
  const modelControlsKey =
    open && isModel && fileId
      ? [fileId, defaultModelColor ?? ""].join("\u0000")
      : "";
  const modelControls = useModelPreviewControls({
    stateKey: modelControlsKey,
    defaultMaterialColor: defaultModelColor,
  });

  React.useEffect(() => {
    if (!textPreviewKey || textSizeError || !fileId) {
      return;
    }

    let cancelled = false;

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

        const content = await response.text();
        if (!cancelled) {
          setTextPreviewState({
            key: textPreviewKey,
            loading: false,
            error: null,
            content,
          });
        }
      } catch (e) {
        if (!cancelled) {
          setTextPreviewState({
            key: textPreviewKey,
            loading: false,
            error:
              e instanceof Error
                ? e.message
                : t("preview.errors.loadFailed", { ns: "files", error: "" }),
            content: null,
          });
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [fileId, t, textPreviewKey, textSizeError, token]);

  if (!open || !fileId || !fileName) {
    return null;
  }

  if (fileType !== "pdf" && fileType !== "text" && fileType !== "model") {
    return null;
  }

  const modelHeaderActions = isModel
    ? (
      <Stack direction="row" spacing={0.5} alignItems="center">
        <Tooltip
          title={t("preview.model.actions.cycleLighting", {
            ns: "files",
            preset: t(`preview.model.lighting.${modelControls.lightingPreset}`, { ns: "files" }),
          })}
        >
          <IconButton onClick={modelControls.cycleLightingPreset}>
            <WbSunny />
          </IconButton>
        </Tooltip>

        <Tooltip
          title={t("preview.model.actions.toggleShadows", {
            ns: "files",
            state: t(
              modelControls.shadowsEnabled
                ? "preview.model.states.on"
                : "preview.model.states.off",
              { ns: "files" },
            ),
          })}
        >
          <IconButton
            color={modelControls.shadowsEnabled ? "primary" : "default"}
            onClick={modelControls.toggleShadowsEnabled}
          >
            <FilterDrama />
          </IconButton>
        </Tooltip>

        <Tooltip
          title={t("preview.model.actions.cycleSurface", {
            ns: "files",
            preset: t(`preview.model.surface.${modelControls.surfacePreset}`, { ns: "files" }),
          })}
        >
          <IconButton onClick={modelControls.cycleSurfacePreset}>
            <Texture />
          </IconButton>
        </Tooltip>

        <Tooltip title={t("preview.model.actions.flipModel", { ns: "files" })}>
          <IconButton onClick={modelControls.requestFlip}>
            <SwapVert />
          </IconButton>
        </Tooltip>

        <Tooltip title={t("preview.model.actions.autoOrient", { ns: "files" })}>
          <IconButton onClick={modelControls.requestAutoOrient}>
            <AutoFixHigh />
          </IconButton>
        </Tooltip>

        <Tooltip title={t("preview.model.actions.autoAlign", { ns: "files" })}>
          <IconButton onClick={modelControls.requestAutoAlign}>
            <VerticalAlignBottom />
          </IconButton>
        </Tooltip>

        <Tooltip title={t("preview.model.actions.togglePalette", { ns: "files" })}>
          <IconButton
            onClick={(event) => {
              modelControls.togglePaletteAnchor(event.currentTarget);
            }}
          >
            <ColorLens />
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
          {textPreview.loading && (
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

          {!textPreview.loading && textPreview.error && (
            <Box
              sx={{
                flex: 1,
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                px: 2,
              }}
            >
              <Typography color="error">{textPreview.error}</Typography>
            </Box>
          )}

          {!textPreview.loading && !textPreview.error && textPreview.content !== null && (
            <ReadOnlyTextViewer
              title={fileName}
              fileName={fileName}
              contentType={contentType}
              textContent={textPreview.content}
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
              materialColor={modelControls.materialColor}
              autoAlignToken={modelControls.autoAlignToken}
              autoOrientToken={modelControls.autoOrientToken}
              flipToken={modelControls.flipToken}
              lightingPreset={modelControls.lightingPreset}
              shadowsEnabled={modelControls.shadowsEnabled}
              surfacePreset={modelControls.surfacePreset}
            />
          </Box>
        </Box>
      )}

      {isModel && (
        <Popover
          open={Boolean(modelControls.paletteAnchorEl)}
          anchorEl={modelControls.paletteAnchorEl}
          onClose={modelControls.closePalette}
          anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
          transformOrigin={{ vertical: "top", horizontal: "right" }}
          sx={{ mt: 0.5 }}
        >
          <Box
            sx={{
              alignItems: "center",
              display: "flex",
              flexWrap: "wrap",
              gap: 0.5,
              maxWidth: 360,
              px: 1,
              py: 0.75,
            }}
          >
            <Tooltip title={t("preview.model.actions.resetColor", { ns: "files" })}>
              <IconButton
                size="small"
                sx={{
                  height: 30,
                  width: 30,
                }}
                onClick={() => {
                  modelControls.setMaterialColor(null);
                  modelControls.closePalette();
                }}
              >
                <FormatColorReset fontSize="small" />
              </IconButton>
            </Tooltip>

            {paletteColors.map((paletteOption) => (
              <IconButton
                key={paletteOption.id}
                onClick={() => {
                  modelControls.setMaterialColor(paletteOption.color);
                  modelControls.closePalette();
                }}
              >
                <Box
                  sx={{
                    backgroundColor: paletteOption.color,
                    border: 1,
                    borderColor:
                      modelControls.materialColor === paletteOption.color ? "text.primary" : "divider",
                    borderRadius: "50%",
                    height: 18,
                    width: 18,
                  }}
                />
              </IconButton>
            ))}
          </Box>
        </Popover>
      )}
    </PreviewModal>
  );
};
