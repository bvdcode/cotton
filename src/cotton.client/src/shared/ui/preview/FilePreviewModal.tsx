import React, { lazy, Suspense } from "react";
import {
  Box,
  CircularProgress,
  IconButton,
  Popover,
  Stack,
  Tooltip,
  useTheme,
} from "@mui/material";
import type { Theme } from "@mui/material/styles";
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
import { PreviewModal } from "./PreviewModal";
import { useModelPreviewControls } from "./hooks/useModelPreviewControls";
import type { FileType } from "@shared/utils/fileTypes";

const PdfPreview = lazy(() =>
  import("./PdfPreview").then((module) => ({
    default: module.PdfPreview,
  })),
);

const TextPreview = lazy(() =>
  import("./TextPreview").then((module) => ({
    default: module.TextPreview,
  })),
);

const ModelPreview = lazy(() =>
  import("./ModelPreview").then((module) => ({
    default: module.ModelPreview,
  })),
);

const PreviewFallback: React.FC = () => (
  <Box
    alignItems="center"
    display="flex"
    height="100%"
    justifyContent="center"
    minHeight={120}
    width="100%"
  >
    <CircularProgress size={24} />
  </Box>
);

interface FilePreviewModalProps {
  isOpen: boolean;
  fileId: string | null;
  fileName: string | null;
  fileType: FileType | null;
  fileSizeBytes: number | null;
  onClose: () => void;
  onSaved?: () => void;
}

type PaletteColor = {
  id: string;
  color: string;
};

type FilePreviewSource = {
  kind: "fileId";
  fileId: string;
};

type ModelPreviewControls = ReturnType<typeof useModelPreviewControls>;

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
  const theme = useTheme();
  const isModel = fileType === "model";
  const defaultModelColor = React.useMemo(() => theme.palette.error.main, [theme]);
  const paletteColors = React.useMemo(() => buildPaletteColors(theme), [theme]);
  const fileSource = React.useMemo<FilePreviewSource | null>(
    () => (fileId ? { kind: "fileId", fileId } : null),
    [fileId],
  );
  const modelControls = useModelPreviewControls({
    stateKey: getModelControlsKey(isOpen, isModel, fileId, defaultModelColor),
    defaultMaterialColor: defaultModelColor,
  });

  if (!isOpen || !fileId || !fileName) {
    return null;
  }

  return (
    <PreviewModal
      open={isOpen}
      onClose={onClose}
      layout={getPreviewLayout(fileType, isModel)}
      title={getPreviewTitle(fileName, fileType, isModel)}
      forceFullScreen={isModel}
      headerActions={
        isModel ? <ModelHeaderActions modelControls={modelControls} /> : undefined
      }
    >
      <FilePreviewBody
        fileId={fileId}
        fileName={fileName}
        fileType={fileType}
        fileSizeBytes={fileSizeBytes}
        fileSource={fileSource}
        modelControls={modelControls}
        onSaved={onSaved}
      />
      {isModel && (
        <ModelPalettePopover
          paletteColors={paletteColors}
          modelControls={modelControls}
        />
      )}
    </PreviewModal>
  );
};

const buildPaletteColors = (theme: Theme): PaletteColor[] => [
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
];

const getModelControlsKey = (
  isOpen: boolean,
  isModel: boolean,
  fileId: string | null,
  defaultModelColor: string,
) => (isOpen && isModel && fileId ? [fileId, defaultModelColor].join("\u0000") : "");

const getPreviewLayout = (fileType: FileType | null, isModel: boolean) =>
  fileType === "pdf" || isModel ? "header" : "overlay";

const getPreviewTitle = (
  fileName: string,
  fileType: FileType | null,
  isModel: boolean,
) => (fileType === "pdf" || isModel ? fileName : undefined);

type ModelHeaderActionsProps = {
  modelControls: ModelPreviewControls;
};

const ModelHeaderActions = ({ modelControls }: ModelHeaderActionsProps) => {
  const { t } = useTranslation(["files"]);

  return (
    <Stack direction="row" spacing={0.5} alignItems="center">
      <Tooltip
        title={t("preview.model.actions.cycleLighting", {
          preset: t("preview.model.lighting." + modelControls.lightingPreset),
        })}
      >
        <IconButton onClick={modelControls.cycleLightingPreset}>
          <WbSunny />
        </IconButton>
      </Tooltip>
      <Tooltip
        title={t("preview.model.actions.toggleShadows", {
          state: t(
            modelControls.shadowsEnabled
              ? "preview.model.states.on"
              : "preview.model.states.off",
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
          preset: t("preview.model.surface." + modelControls.surfacePreset),
        })}
      >
        <IconButton onClick={modelControls.cycleSurfacePreset}>
          <Texture />
        </IconButton>
      </Tooltip>
      <Tooltip title={t("preview.model.actions.flipModel")}>
        <IconButton onClick={modelControls.requestFlip}>
          <SwapVert />
        </IconButton>
      </Tooltip>
      <Tooltip title={t("preview.model.actions.autoOrient")}>
        <IconButton onClick={modelControls.requestAutoOrient}>
          <AutoFixHigh />
        </IconButton>
      </Tooltip>
      <Tooltip title={t("preview.model.actions.autoAlign")}>
        <IconButton onClick={modelControls.requestAutoAlign}>
          <VerticalAlignBottom />
        </IconButton>
      </Tooltip>
      <Tooltip title={t("preview.model.actions.togglePalette")}>
        <IconButton
          onClick={(event) => {
            modelControls.togglePaletteAnchor(event.currentTarget);
          }}
        >
          <ColorLens />
        </IconButton>
      </Tooltip>
    </Stack>
  );
};

type FilePreviewBodyProps = {
  fileId: string;
  fileName: string;
  fileType: FileType | null;
  fileSizeBytes: number | null;
  fileSource: FilePreviewSource | null;
  modelControls: ModelPreviewControls;
  onSaved?: () => void;
};

const FilePreviewBody = ({
  fileId,
  fileName,
  fileType,
  fileSizeBytes,
  fileSource,
  modelControls,
  onSaved,
}: FilePreviewBodyProps) => (
  <>
    {fileType === "pdf" && fileSource && (
      <Suspense fallback={<PreviewFallback />}>
        <PdfPreview
          source={fileSource}
          fileName={fileName}
          fileSizeBytes={fileSizeBytes}
        />
      </Suspense>
    )}
    {fileType === "text" && (
      <Suspense fallback={<PreviewFallback />}>
        <TextPreview
          nodeFileId={fileId}
          fileName={fileName}
          fileSizeBytes={fileSizeBytes}
          onSaved={onSaved}
        />
      </Suspense>
    )}
    {fileType === "model" && fileSource && (
      <ModelPreviewBody
        fileName={fileName}
        fileSizeBytes={fileSizeBytes}
        fileSource={fileSource}
        modelControls={modelControls}
      />
    )}
  </>
);

type ModelPreviewBodyProps = {
  fileName: string;
  fileSizeBytes: number | null;
  fileSource: FilePreviewSource;
  modelControls: ModelPreviewControls;
};

const ModelPreviewBody = ({
  fileName,
  fileSizeBytes,
  fileSource,
  modelControls,
}: ModelPreviewBodyProps) => (
  <Box
    sx={{
      display: "flex",
      flex: 1,
      flexDirection: "column",
      minHeight: 0,
    }}
  >
    <Box sx={{ flex: 1, minHeight: 0 }}>
      <Suspense fallback={<PreviewFallback />}>
        <ModelPreview
          source={fileSource}
          fileName={fileName}
          fileSizeBytes={fileSizeBytes}
          materialColor={modelControls.materialColor}
          autoAlignToken={modelControls.autoAlignToken}
          autoOrientToken={modelControls.autoOrientToken}
          flipToken={modelControls.flipToken}
          lightingPreset={modelControls.lightingPreset}
          shadowsEnabled={modelControls.shadowsEnabled}
          surfacePreset={modelControls.surfacePreset}
        />
      </Suspense>
    </Box>
  </Box>
);

type ModelPalettePopoverProps = {
  paletteColors: PaletteColor[];
  modelControls: ModelPreviewControls;
};

const ModelPalettePopover = ({
  paletteColors,
  modelControls,
}: ModelPalettePopoverProps) => {
  const { t } = useTranslation(["files"]);

  return (
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
        <Tooltip title={t("preview.model.actions.resetColor")}>
          <IconButton
            sx={{ height: 34, width: 34 }}
            onClick={() => {
              modelControls.setMaterialColor(null);
              modelControls.closePalette();
            }}
          >
            <FormatColorReset />
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
                  modelControls.materialColor === paletteOption.color
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
  );
};
