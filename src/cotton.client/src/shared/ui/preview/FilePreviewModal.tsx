import React, { lazy, Suspense } from "react";
import {
  Box,
  CircularProgress,
  IconButton,
  Popover,
  Stack,
  Tooltip,
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
import { PreviewModal } from "./PreviewModal";
import { useModelPreviewControls } from "./hooks/useModelPreviewControls";
import {
  useDefaultModelColor,
  useModelPaletteColors,
  type PaletteColor,
} from "./modelPalette";
import type { FileType } from "@shared/utils/fileTypes";
import type { NodeFileManifestDto } from "../../api/nodesApi";

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
  file?: NodeFileManifestDto | null;
  onClose: () => void;
  onSaved?: () => void;
}

type FilePreviewSource = {
  kind: "fileId";
  fileId: string;
};

type ModelPreviewControls = ReturnType<typeof useModelPreviewControls>;

/**
 * Shared file preview modal component
 * Displays PDF, Text, or Model preview based on file type
 */
export const FilePreviewModal: React.FC<FilePreviewModalProps> = ({
  isOpen,
  fileId,
  fileName,
  fileType,
  fileSizeBytes,
  file,
  onClose,
  onSaved,
}) => {
  const isModel = fileType === "model";
  const defaultModelColor = useDefaultModelColor();
  const paletteColors = useModelPaletteColors();
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
        sourceFile={file}
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
  sourceFile?: NodeFileManifestDto | null;
  fileSource: FilePreviewSource | null;
  modelControls: ModelPreviewControls;
  onSaved?: () => void;
};

const FilePreviewBody = ({
  fileId,
  fileName,
  fileType,
  fileSizeBytes,
  sourceFile,
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
          sourceFile={sourceFile}
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
