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
  FormatColorReset,
  VerticalAlignBottom,
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

  React.useEffect(() => {
    if (!isOpen || !isModel) {
      setPaletteAnchorEl(null);
      setMaterialColor(null);
      return;
    }

    setPaletteAnchorEl(null);
    setMaterialColor(null);
  }, [fileId, isModel, isOpen]);

  if (!isOpen || !fileId || !fileName) {
    return null;
  }

  const modelHeaderActions = isModel
    ? (
      <Stack direction="row" spacing={0.5} alignItems="center">
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
