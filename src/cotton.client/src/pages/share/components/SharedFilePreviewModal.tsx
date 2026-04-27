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

type LightingPreset = "balanced" | "studio" | "dramatic";
type SurfacePreset =
  | "original"
  | "matte"
  | "glossy"
  | "metal"
  | "satin"
  | "smooth";

const LIGHTING_PRESET_ORDER: ReadonlyArray<LightingPreset> = [
  "balanced",
  "studio",
  "dramatic",
];

const SURFACE_PRESET_ORDER: ReadonlyArray<SurfacePreset> = [
  "original",
  "matte",
  "glossy",
  "smooth",
  "metal",
  "satin",
];

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
    return theme.palette.mode === "dark"
      ? theme.palette.grey[700]
      : null;
  }, [theme]);

  const [textContent, setTextContent] = React.useState<string | null>(null);
  const [loadingText, setLoadingText] = React.useState<boolean>(false);
  const [textError, setTextError] = React.useState<string | null>(null);
  const [paletteAnchorEl, setPaletteAnchorEl] = React.useState<HTMLElement | null>(null);
  const [materialColor, setMaterialColor] = React.useState<string | null>(
    defaultModelColor,
  );
  const [autoAlignToken, setAutoAlignToken] = React.useState<number>(0);
  const [autoOrientToken, setAutoOrientToken] = React.useState<number>(0);
  const [flipToken, setFlipToken] = React.useState<number>(0);
  const [lightingPreset, setLightingPreset] = React.useState<LightingPreset>("balanced");
  const [surfacePreset, setSurfacePreset] = React.useState<SurfacePreset>("metal");
  const [shadowsEnabled, setShadowsEnabled] = React.useState<boolean>(true);

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

  const cycleLightingPreset = React.useCallback(() => {
    setLightingPreset((currentPreset) => {
      const currentIndex = LIGHTING_PRESET_ORDER.indexOf(currentPreset);
      const nextIndex = (currentIndex + 1) % LIGHTING_PRESET_ORDER.length;
      return LIGHTING_PRESET_ORDER[nextIndex];
    });
  }, []);

  const cycleSurfacePreset = React.useCallback(() => {
    setSurfacePreset((currentPreset) => {
      const currentIndex = SURFACE_PRESET_ORDER.indexOf(currentPreset);
      const nextIndex = (currentIndex + 1) % SURFACE_PRESET_ORDER.length;
      return SURFACE_PRESET_ORDER[nextIndex];
    });
  }, []);

  React.useEffect(() => {
    if (!open || !isModel) {
      setPaletteAnchorEl(null);
      setMaterialColor(defaultModelColor);
      setLightingPreset("balanced");
      setSurfacePreset("metal");
      setShadowsEnabled(true);
      return;
    }

    setPaletteAnchorEl(null);
    setMaterialColor(defaultModelColor);
    setLightingPreset("balanced");
    setSurfacePreset("metal");
    setShadowsEnabled(true);
  }, [defaultModelColor, fileId, isModel, open]);

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
        <Tooltip
          title={t("preview.model.actions.cycleLighting", {
            ns: "files",
            preset: t(`preview.model.lighting.${lightingPreset}`, { ns: "files" }),
          })}
        >
          <IconButton onClick={cycleLightingPreset}>
            <WbSunny />
          </IconButton>
        </Tooltip>

        <Tooltip
          title={t("preview.model.actions.toggleShadows", {
            ns: "files",
            state: t(
              shadowsEnabled
                ? "preview.model.states.on"
                : "preview.model.states.off",
              { ns: "files" },
            ),
          })}
        >
          <IconButton
            color={shadowsEnabled ? "primary" : "default"}
            onClick={() => setShadowsEnabled((currentState) => !currentState)}
          >
            <FilterDrama />
          </IconButton>
        </Tooltip>

        <Tooltip
          title={t("preview.model.actions.cycleSurface", {
            ns: "files",
            preset: t(`preview.model.surface.${surfacePreset}`, { ns: "files" }),
          })}
        >
          <IconButton onClick={cycleSurfacePreset}>
            <Texture />
          </IconButton>
        </Tooltip>

        <Tooltip title={t("preview.model.actions.flipModel", { ns: "files" })}>
          <IconButton onClick={() => setFlipToken((value) => value + 1)}>
            <SwapVert />
          </IconButton>
        </Tooltip>

        <Tooltip title={t("preview.model.actions.autoOrient", { ns: "files" })}>
          <IconButton onClick={() => setAutoOrientToken((value) => value + 1)}>
            <AutoFixHigh />
          </IconButton>
        </Tooltip>

        <Tooltip title={t("preview.model.actions.autoAlign", { ns: "files" })}>
          <IconButton onClick={() => setAutoAlignToken((value) => value + 1)}>
            <VerticalAlignBottom />
          </IconButton>
        </Tooltip>

        <Tooltip title={t("preview.model.actions.togglePalette", { ns: "files" })}>
          <IconButton
            onClick={(event) => {
              setPaletteAnchorEl((current) => (current ? null : event.currentTarget));
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
              flipToken={flipToken}
              lightingPreset={lightingPreset}
              shadowsEnabled={shadowsEnabled}
              surfacePreset={surfacePreset}
            />
          </Box>
        </Box>
      )}

      {isModel && (
        <Popover
          open={Boolean(paletteAnchorEl)}
          anchorEl={paletteAnchorEl}
          onClose={() => setPaletteAnchorEl(null)}
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
                  setMaterialColor(null);
                  setPaletteAnchorEl(null);
                }}
              >
                <FormatColorReset fontSize="small" />
              </IconButton>
            </Tooltip>

            {paletteColors.map((paletteOption) => (
              <IconButton
                key={paletteOption.id}
                onClick={() => {
                  setMaterialColor(paletteOption.color);
                  setPaletteAnchorEl(null);
                }}
              >
                <Box
                  sx={{
                    backgroundColor: paletteOption.color,
                    border: 1,
                    borderColor:
                      materialColor === paletteOption.color ? "text.primary" : "divider",
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
