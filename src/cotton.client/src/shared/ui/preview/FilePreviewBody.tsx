import { useMemo } from "react";
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
import { useReadablePreviewSource } from "./hooks/useReadablePreviewSource";
import { PdfPreview } from "./PdfPreview";
import { TextPreview } from "./TextPreview";
import { ModelPreview } from "./ModelPreview";
import type { ModelPreviewSource } from "./modelPreviewTypes";
import { AudioPreview } from "./AudioPreview";
import { FilePreviewUnavailable } from "./FilePreviewUnavailable";

type ModelPreviewControls = ReturnType<typeof useModelPreviewControls>;

type FilePreviewBodyProps = {
  fileId: string;
  fileName: string;
  fileType: FileType | null;
  fileSizeBytes: number | null;
  sourceFile?: NodeFileManifestDto | null;
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
  modelControls,
  onSaved,
  onDownload,
}: FilePreviewBodyProps) => {
  const { t } = useTranslation("files");
  const encrypted = sourceFile ? isFileEncrypted(sourceFile.metadata) : false;
  const readable = useReadablePreviewSource(
    encrypted && fileType !== "other" && fileType !== null
      ? (sourceFile ?? null)
      : null,
  );
  const fileSource = useMemo<ModelPreviewSource>(() => {
    if (readable.url) {
      return { kind: "url", url: readable.url };
    }
    return { kind: "fileId", fileId };
  }, [fileId, readable.url]);
  const pdfSource = useMemo(() => {
    switch (fileSource.kind) {
      case "fileId":
        return fileSource;
      case "url":
        return { kind: "objectUrl" as const, url: fileSource.url };
    }
  }, [fileSource]);
  const download = () => {
    void onDownload(fileId, fileName);
  };

  if (fileType === "other") {
    const originalType = getFileTypeInfo(
      fileName,
      sourceFile?.contentType,
    ).type;
    let limit: number | null = null;
    if (encrypted) {
      limit = CLIENT_ENCRYPTION_BLOB_PIPELINE_MAX_BYTES;
      if (originalType === "pdf") {
        limit = previewConfig.MAX_PDF_PREVIEW_SIZE_BYTES;
      }
    } else if (originalType === "text") {
      limit = previewConfig.MAX_TEXT_PREVIEW_SIZE_BYTES;
    }
    return (
      <FilePreviewUnavailable
        message={
          limit !== null && (fileSizeBytes ?? 0) > limit
            ? t("preview.errors.fileTooLarge", {
                size: formatBytes(fileSizeBytes ?? 0),
                maxSize: formatBytes(limit),
              })
            : undefined
        }
        onDownload={download}
      />
    );
  }

  if (encrypted && (readable.failed || !readable.url)) {
    return (
      <Box
        display="flex"
        flex={1}
        minHeight={0}
        alignItems="center"
        justifyContent="center"
      >
        {readable.failed ? (
          <FilePreviewUnavailable
            message={t("preview.errors.loadFailed", { error: "" })}
            onDownload={download}
          />
        ) : (
          <CircularProgress />
        )}
      </Box>
    );
  }

  switch (fileType) {
    case "image":
      return (
        <Box
          component="img"
          src={readable.url ?? undefined}
          alt={fileName}
          width="100%"
          height="100%"
          minHeight={0}
          sx={{ objectFit: "contain" }}
          onError={readable.onSourceError}
        />
      );
    case "video":
      return (
        <Box
          component="video"
          src={readable.url ?? undefined}
          controls
          playsInline
          autoPlay
          preload="metadata"
          width="100%"
          height="100%"
          minHeight={0}
          sx={{ objectFit: "contain" }}
          onError={readable.onSourceError}
        />
      );
    case "audio":
      return (
        <Box display="flex" flex={1} alignItems="center" px={3}>
          <AudioPreview
            nodeFileId={fileId}
            fileName={fileName}
            sourceUrl={readable.url ?? undefined}
            onError={readable.onSourceError}
          />
        </Box>
      );
    case "pdf":
      return (
        <PdfPreview
          source={pdfSource}
          fileName={fileName}
          fileSizeBytes={fileSizeBytes}
        />
      );
    case "text":
      return (
        <TextPreview
          key={readable.url ?? fileId}
          nodeFileId={fileId}
          fileName={fileName}
          fileSizeBytes={fileSizeBytes}
          sourceFile={sourceFile}
          previewUrl={readable.url ?? undefined}
          onSaved={onSaved}
        />
      );
    case "model":
      return (
        <ModelPreviewBody
          fileName={fileName}
          fileSizeBytes={fileSizeBytes}
          fileSource={fileSource}
          contentType={sourceFile?.contentType}
          modelControls={modelControls}
        />
      );
    case "document":
    case "archive":
      return <FilePreviewUnavailable onDownload={download} />;
    case null:
      return null;
  }
};

type ModelPreviewBodyProps = {
  fileName: string;
  fileSizeBytes: number | null;
  fileSource: ModelPreviewSource;
  contentType?: string | null;
  modelControls: ModelPreviewControls;
};

const ModelPreviewBody = ({
  fileName,
  fileSizeBytes,
  fileSource,
  contentType,
  modelControls,
}: ModelPreviewBodyProps) => (
  <Box display="flex" flex={1} flexDirection="column" minHeight={0}>
    <Box flex={1} minHeight={0}>
      <ModelPreview
        source={fileSource}
        fileName={fileName}
        contentType={contentType}
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
