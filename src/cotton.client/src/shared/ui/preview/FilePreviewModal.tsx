import React from "react";
import { Box, IconButton, Popover, Stack, Tooltip } from "@mui/material";
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
import { FilePreviewBody } from "./FilePreviewBody";

interface FilePreviewModalProps {
  isOpen: boolean;
  fileId: string | null;
  fileName: string | null;
  fileType: FileType | null;
  fileSizeBytes: number | null;
  file?: NodeFileManifestDto | null;
  onClose: () => void;
  onDownload: (fileId: string, fileName: string) => Promise<void>;
  onSaved?: () => void;
}

type ModelPreviewControls = ReturnType<typeof useModelPreviewControls>;

/**
 * Shared file preview modal component
 * Displays a supported file using its readable source.
 */
export const FilePreviewModal: React.FC<FilePreviewModalProps> = ({
  isOpen,
  fileId,
  fileName,
  fileType,
  fileSizeBytes,
  file,
  onClose,
  onDownload,
  onSaved,
}) => {
  const isModel = fileType === "model";
  const defaultModelColor = useDefaultModelColor();
  const paletteColors = useModelPaletteColors();
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
      layout={getPreviewLayout(fileType)}
      title={getPreviewLayout(fileType) === "header" ? fileName : undefined}
      forceFullScreen={isModel}
      headerActions={
        isModel ? (
          <ModelHeaderActions modelControls={modelControls} />
        ) : undefined
      }
    >
      <FilePreviewBody
        fileId={fileId}
        fileName={fileName}
        fileType={fileType}
        fileSizeBytes={fileSizeBytes}
        sourceFile={file}
        modelControls={modelControls}
        onSaved={onSaved}
        onDownload={onDownload}
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
) =>
  isOpen && isModel && fileId ? [fileId, defaultModelColor].join("\u0000") : "";

const getPreviewLayout = (fileType: FileType | null): "header" | "overlay" => {
  switch (fileType) {
    case "pdf":
    case "model":
    case "document":
    case "archive":
    case "other":
      return "header";
    case "text":
    case "image":
    case "video":
    case "audio":
    case null:
      return "overlay";
  }
};

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
