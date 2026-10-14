import { Box, CircularProgress } from "@mui/material";
import { useTranslation } from "react-i18next";
import type { NodeFileManifestDto } from "../../api/nodesApi";
import type { FileType } from "../../utils/fileTypes";
import { getFileTypeInfo } from "../../utils/fileTypes";
import { previewConfig } from "../../config/previewConfig";
import { formatBytes } from "../../utils/formatBytes";
import {
  CLIENT_ENCRYPTION_BLOB_PIPELINE_MAX_BYTES,
  isFileEncrypted,
} from "../../crypto";
import type { useModelPreviewControls } from "./hooks/useModelPreviewControls";
import { useImagePreviewSource } from "./hooks/useImagePreviewSource";
import { PdfPreview } from "./PdfPreview";
import { TextPreview } from "./TextPreview";
import { ModelPreview } from "./ModelPreview";
import { FilePreviewUnavailable } from "./FilePreviewUnavailable";

export type FilePreviewSource = { kind: "fileId"; fileId: string };
type ModelPreviewControls = ReturnType<typeof useModelPreviewControls>;

type FilePreviewBodyProps = {
  fileId: string;
  fileName: string;
  fileType: FileType | null;
  fileSizeBytes: number | null;
  sourceFile?: NodeFileManifestDto | null;
  fileSource: FilePreviewSource | null;
  modelControls: ModelPreviewControls;
  onSaved?: () => void;
  onDownload: (fileId: string, fileName: string) => Promise<void>;
};

export const FilePreviewBody = ({
  fileId,
  fileName,
  fileType,
  fileSizeBytes,
  sourceFile,
  fileSource,
  modelControls,
  onSaved,
  onDownload,
}: FilePreviewBodyProps) => {
  const { t } = useTranslation("files");
  const image = useImagePreviewSource(
    fileType === "image" ? (sourceFile ?? null) : null,
  );
  const textPreviewLimit =
    sourceFile && isFileEncrypted(sourceFile.metadata)
      ? CLIENT_ENCRYPTION_BLOB_PIPELINE_MAX_BYTES
      : previewConfig.MAX_TEXT_PREVIEW_SIZE_BYTES;
  const oversizedText =
    getFileTypeInfo(fileName, sourceFile?.contentType).type === "text" &&
    (fileSizeBytes ?? 0) > textPreviewLimit;
  return (
    <>
      {fileType === "image" && (
        <Box
          display="flex"
          flex={1}
          minHeight={0}
          alignItems="center"
          justifyContent="center"
        >
          {image.failed ? (
            <FilePreviewUnavailable
              message={t("preview.errors.loadFailed", { error: "" })}
              onDownload={() => {
                void onDownload(fileId, fileName);
              }}
            />
          ) : image.url ? (
            <Box
              component="img"
              src={image.url}
              alt={fileName}
              width="100%"
              height="100%"
              minHeight={0}
              sx={{ objectFit: "contain" }}
              onError={image.onImageError}
            />
          ) : (
            <CircularProgress />
          )}
        </Box>
      )}
      {fileType === "other" && (
        <FilePreviewUnavailable
          message={
            oversizedText
              ? t("preview.errors.fileTooLarge", {
                  size: formatBytes(fileSizeBytes ?? 0),
                  maxSize: formatBytes(textPreviewLimit),
                })
              : undefined
          }
          onDownload={() => {
            void onDownload(fileId, fileName);
          }}
        />
      )}
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
          sourceFile={sourceFile}
          onSaved={onSaved}
        />
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
};

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
    </Box>
  </Box>
);
