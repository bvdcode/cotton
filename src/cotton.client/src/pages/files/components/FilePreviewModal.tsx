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
  Rotate90DegreesCw,
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
type SurfacePreset = "original" | "matte" | "glossy";

const LIGHTING_PRESET_ORDER: ReadonlyArray<LightingPreset> = [
  "balanced",
  "studio",
  "dramatic",
];

const SURFACE_PRESET_ORDER: ReadonlyArray<SurfacePreset> = [
  "original",
  "matte",
  "glossy",
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

  const [paletteAnchorEl, setPaletteAnchorEl] = React.useState<HTMLElement | null>(null);
  const [materialColor, setMaterialColor] = React.useState<string | null>(null);
  const [autoAlignToken, setAutoAlignToken] = React.useState<number>(0);
  const [autoOrientToken, setAutoOrientToken] = React.useState<number>(0);
  const [cycleOrientationToken, setCycleOrientationToken] = React.useState<number>(0);
  const [lightingPreset, setLightingPreset] = React.useState<LightingPreset>("balanced");
  const [surfacePreset, setSurfacePreset] = React.useState<SurfacePreset>("original");
  const [shadowsEnabled, setShadowsEnabled] = React.useState<boolean>(true);

  const paletteColors = React.useMemo<Array<{ id: string; color: string }>>(
    () => [
      { id: "neutral", color: theme.palette.grey[500] },
      { id: "green", color: theme.palette.success.dark },
      { id: "primary", color: theme.palette.primary.main },
      { id: "info", color: theme.palette.info.main },
      { id: "warning", color: theme.palette.warning.main },
      { id: "error", color: theme.palette.error.main },
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
      setMaterialColor(null);
      setLightingPreset("balanced");
      setSurfacePreset("original");
      setShadowsEnabled(true);
      return;
    }

    setPaletteAnchorEl(null);
    setMaterialColor(null);
    setLightingPreset("balanced");
    setSurfacePreset("original");
    setShadowsEnabled(true);
  }, [fileId, isModel, isOpen]);

  if (!isOpen || !fileId || !fileName) {
    return null;
  }

  const modelHeaderActions = isModel
    ? (
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

        <Tooltip title={t("preview.model.actions.cycleRotation")}>
          <IconButton onClick={() => setCycleOrientationToken((value) => value + 1)}>
            <Rotate90DegreesCw />
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
              setPaletteAnchorEl((current) => (current ? null : event.currentTarget));
            }}
          >
            <ColorLens
              sx={{ color: materialColor ?? theme.palette.text.primary }}
            />
          </IconButton>
        </Tooltip>
      </Stack>
    )
    : undefined;

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
        <Box sx={{ display: "flex", flex: 1, flexDirection: "column", minHeight: 0 }}>
          <Box sx={{ flex: 1, minHeight: 0 }}>
            <ModelPreview
              source={fileSource}
              fileName={fileName}
              fileSizeBytes={fileSizeBytes}
              materialColor={materialColor}
              autoAlignToken={autoAlignToken}
              autoOrientToken={autoOrientToken}
              cycleOrientationToken={cycleOrientationToken}
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
          <Stack
            direction="row"
            spacing={0.5}
            sx={{
              alignItems: "center",
              px: 1,
              py: 0.75,
            }}
          >
            <Tooltip title={t("preview.model.actions.resetColor")}>
              <IconButton
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
                      materialColor === paletteOption.color ? "text.primary" : "divider",
                    borderRadius: "50%",
                    height: 18,
                    width: 18,
                  }}
                />
              </IconButton>
            ))}
          </Stack>
        </Popover>
      )}
    </PreviewModal>
  );
};
