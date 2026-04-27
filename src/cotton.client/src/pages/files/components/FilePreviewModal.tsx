import React from "react";
import {
  Box,
  Collapse,
  IconButton,
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

  const fileSource = React.useMemo(
    () => (fileId ? { kind: "fileId" as const, fileId } : null),
    [fileId],
  );

  React.useEffect(() => {
    if (!isOpen || !isModel) {
      setIsPaletteOpen(false);
      setMaterialColor(null);
      return;
    }

    setIsPaletteOpen(false);
    setMaterialColor(null);
  }, [fileId, isModel, isOpen]);

  if (!isOpen || !fileId || !fileName) {
    return null;
  }

  const modelHeaderActions = isModel
    ? (
      <Stack direction="row" spacing={0.5} alignItems="center">
        <Tooltip title={t("preview.model.actions.autoOrient")}>
          <IconButton
            size="small"
            onClick={() => setAutoOrientToken((value) => value + 1)}
          >
            <AutoFixHigh fontSize="small" />
          </IconButton>
        </Tooltip>

        <Tooltip title={t("preview.model.actions.autoAlign")}>
          <IconButton
            size="small"
            onClick={() => setAutoAlignToken((value) => value + 1)}
          >
            <VerticalAlignBottom fontSize="small" />
          </IconButton>
        </Tooltip>

        <Tooltip title={t("preview.model.actions.togglePalette")}>
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
                <Tooltip title={t("preview.model.actions.resetColor")}>
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
