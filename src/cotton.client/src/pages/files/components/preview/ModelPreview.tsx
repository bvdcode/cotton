import * as React from "react";
import { Box } from "@mui/material";
import { useTheme } from "@mui/material/styles";
import { useTranslation } from "react-i18next";
import {
  LIGHTING_PRESET_CONFIG,
  resolveQualityMode,
} from "./modelPreviewCore";
import { ModelPreviewScene } from "./components/ModelPreviewScene";
import { ModelPreviewStatus } from "./components/ModelPreviewStatus";
import { useModelPreviewState } from "./hooks/useModelPreviewState";
import {
  type ModelPreviewProps,
} from "./modelPreviewTypes";
import { resolveModelFormat } from "../../utils/modelFormats";

export const ModelPreview: React.FC<ModelPreviewProps> = ({
  source,
  fileName,
  contentType,
  fileSizeBytes,
  materialColor,
  autoAlignToken,
  autoOrientToken,
  flipToken,
  lightingPreset = "dramatic",
  shadowsEnabled = true,
  surfacePreset = "metal",
}) => {
  const { t } = useTranslation(["files"]);
  const theme = useTheme();

  const modelFormat = React.useMemo(
    () => resolveModelFormat(fileName, contentType),
    [contentType, fileName],
  );
  const qualityMode = React.useMemo(
    () => resolveQualityMode(fileSizeBytes),
    [fileSizeBytes],
  );
  const lightingConfig = React.useMemo(
    () => LIGHTING_PRESET_CONFIG[lightingPreset],
    [lightingPreset],
  );
  const sceneBackgroundColor = React.useMemo(() => {
    switch (lightingPreset) {
      case "studio":
        return theme.palette.background.paper;
      case "dramatic":
        return theme.palette.grey[900];
      case "balanced":
      default:
        return theme.palette.background.default;
    }
  }, [lightingPreset, theme]);
  const defaultPreviewColor = React.useMemo<string>(() => {
    return theme.palette.error.main;
  }, [theme]);
  const gridLineColor = React.useMemo(() => {
    return theme.palette.mode === "dark"
      ? theme.palette.grey[700]
      : theme.palette.grey[400];
  }, [theme]);
  const gridSubLineColor = React.useMemo(() => {
    return theme.palette.mode === "dark"
      ? theme.palette.grey[800]
      : theme.palette.grey[200];
  }, [theme]);
  const effectiveMaterialColor = React.useMemo<string | null | undefined>(() => {
    return materialColor === undefined
      ? defaultPreviewColor
      : materialColor;
  }, [defaultPreviewColor, materialColor]);
  const hasColorOverride =
    effectiveMaterialColor !== null &&
    effectiveMaterialColor !== undefined;

  const sourceKey = source.kind === "fileId"
    ? `file:${source.fileId}`
    : `url:${source.url}`;

  const { hasLoadError, isLoading, preparedModel } = useModelPreviewState({
    autoAlignToken,
    autoOrientToken,
    flipToken,
    modelFormat,
    qualityMode,
    sourceKey,
    effectiveMaterialColor,
    hasColorOverride,
    shadowsEnabled,
    surfacePreset,
  });

  if (!modelFormat) {
    return (
      <ModelPreviewStatus
        message={t("preview.errors.modelUnsupportedType", { ns: "files" })}
      />
    );
  }

  return (
    <Box
      sx={{
        width: "100%",
        height: "100%",
        minHeight: 0,
        position: "relative",
      }}
    >
      {(isLoading || hasLoadError || !preparedModel) && (
        <ModelPreviewStatus
          absolute
          loading={isLoading}
          color={isLoading ? "text.secondary" : "error"}
          message={isLoading
            ? t("preview.model.loading", { ns: "files" })
            : t("preview.errors.modelLoadFailed", { ns: "files" })}
        />
      )}

      {!hasLoadError && preparedModel && (
        <ModelPreviewScene
          gridLineColor={gridLineColor}
          gridSubLineColor={gridSubLineColor}
          lightingConfig={lightingConfig}
          preparedModel={preparedModel}
          sceneBackgroundColor={sceneBackgroundColor}
          shadowsEnabled={shadowsEnabled}
        />
      )}
    </Box>
  );
};
