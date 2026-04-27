import React from "react";
import {
  Box,
  IconButton,
  Popover,
  Stack,
  Tooltip,
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
import { PreviewModal, PdfPreview, TextPreview, ModelPreview } from "./preview";
import type { FileType } from "../utils/fileTypes";

interface FilePreviewModalProps {
  isOpen: boolean;
  fileId: string | null;
  fileName: string | null;
  fileType: FileType | null;
  fileSizeBytes: number | null;
  onClose: () => void;
  onSaved?: () => void;
}

type LightingPreset = "balanced" | "studio" | "dramatic";
type SurfacePreset =
  | "original"
  | "metal"
  | "smooth";

const LIGHTING_PRESET_ORDER: ReadonlyArray<LightingPreset> = [
  "balanced",
  "studio",
  "dramatic",
];

const SURFACE_PRESET_ORDER: ReadonlyArray<SurfacePreset> = [
  "original",
  "metal",
  "smooth",
];

/**
 * Shared file preview modal component
 * Displays PDF or Text preview based on file type
 */
export const FilePreviewModal: React.FC<FilePreviewModalProps> = ({
  isOpen,
  fileId,
  fileName,
  fileType,
  fileSizeBytes,
  onClose,
  onSaved,
}) => {
  const { t } = useTranslation(["files"]);
  const theme = useTheme();
  const isModel = fileType === "model";
  const defaultModelColor = React.useMemo<string | null>(() => {
    return theme.palette.mode === "dark"
      ? theme.palette.grey[700]
      : null;
  }, [theme]);

  const [paletteAnchorEl, setPaletteAnchorEl] =
    React.useState<HTMLElement | null>(null);
  const [materialColor, setMaterialColor] = React.useState<string | null>(
    defaultModelColor,
  );
  const [autoAlignToken, setAutoAlignToken] = React.useState<number>(0);
  const [autoOrientToken, setAutoOrientToken] = React.useState<number>(0);
  const [flipToken, setFlipToken] = React.useState<number>(0);
  const [lightingPreset, setLightingPreset] =
    React.useState<LightingPreset>("balanced");
  const [surfacePreset, setSurfacePreset] =
    React.useState<SurfacePreset>("metal");
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

  const fileSource = React.useMemo(
    () => (fileId ? { kind: "fileId" as const, fileId } : null),
    [fileId],
  );

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
    if (!isOpen || !isModel) {
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
  }, [defaultModelColor, fileId, isModel, isOpen]);

  if (!isOpen || !fileId || !fileName) {
    return null;
  }

  const modelHeaderActions = isModel ? (
    <Stack direction="row" spacing={0.5} alignItems="center">
      <Tooltip
        title={t("preview.model.actions.cycleLighting", {
          preset: t(`preview.model.lighting.${lightingPreset}`),
        })}
      >
        <IconButton onClick={cycleLightingPreset}>
          <WbSunny />
        </IconButton>
      </Tooltip>

      <Tooltip
        title={t("preview.model.actions.toggleShadows", {
          state: t(
            shadowsEnabled
              ? "preview.model.states.on"
              : "preview.model.states.off",
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
          preset: t(`preview.model.surface.${surfacePreset}`),
        })}
      >
        <IconButton onClick={cycleSurfacePreset}>
          <Texture />
        </IconButton>
      </Tooltip>

      <Tooltip title={t("preview.model.actions.flipModel")}>
        <IconButton onClick={() => setFlipToken((value) => value + 1)}>
          <SwapVert />
        </IconButton>
      </Tooltip>

      <Tooltip title={t("preview.model.actions.autoOrient")}>
        <IconButton onClick={() => setAutoOrientToken((value) => value + 1)}>
          <AutoFixHigh />
        </IconButton>
      </Tooltip>

      <Tooltip title={t("preview.model.actions.autoAlign")}>
        <IconButton onClick={() => setAutoAlignToken((value) => value + 1)}>
          <VerticalAlignBottom />
        </IconButton>
      </Tooltip>

      <Tooltip title={t("preview.model.actions.togglePalette")}>
        <IconButton
          onClick={(event) => {
            setPaletteAnchorEl((current) =>
              current ? null : event.currentTarget,
            );
          }}
        >
          <ColorLens />
        </IconButton>
      </Tooltip>
    </Stack>
  ) : undefined;

  return (
    <PreviewModal
      open={isOpen}
      onClose={onClose}
      layout={fileType === "pdf" || isModel ? "header" : "overlay"}
      title={fileType === "pdf" || isModel ? fileName : undefined}
      forceFullScreen={isModel}
      headerActions={modelHeaderActions}
    >
      {fileType === "pdf" && fileSource && (
        <PdfPreview
          source={fileSource}
          fileName={fileName}
          fileSizeBytes={fileSizeBytes}
        />
      )}
      {fileType === "text" && (
        <TextPreview
          nodeFileId={fileId}
          fileName={fileName}
          fileSizeBytes={fileSizeBytes}
          onSaved={onSaved}
        />
      )}

      {isModel && fileSource && (
        <Box
          sx={{
            display: "flex",
            flex: 1,
            flexDirection: "column",
            minHeight: 0,
          }}
        >
          <Box sx={{ flex: 1, minHeight: 0 }}>
            <ModelPreview
              source={fileSource}
              fileName={fileName}
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
            <Tooltip title={t("preview.model.actions.resetColor")}>
              <IconButton
                sx={{
                  height: 34,
                  width: 34,
                }}
                onClick={() => {
                  setMaterialColor(null);
                  setPaletteAnchorEl(null);
                }}
              >
                <FormatColorReset />
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
                      materialColor === paletteOption.color
                        ? "text.primary"
                        : "divider",
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
