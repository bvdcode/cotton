import { Box, CircularProgress } from "@mui/material";
import { useTranslation } from "react-i18next";
import type { TFunction } from "i18next";
import type { NodeFileManifestDto } from "../../api/nodesApi";
import type { FileType } from "../../utils/fileTypes";
import { getFileTypeInfo } from "../../utils/fileTypes";
import { previewConfig } from "../../config/previewConfig";
import { formatBytes } from "../../utils/formatBytes";
import { CLIENT_ENCRYPTION_BLOB_PIPELINE_MAX_BYTES } from "../../crypto";
import type { useModelPreviewControls } from "./hooks/useModelPreviewControls";
import { useFilePreviewSources } from "./hooks/useFilePreviewSources";
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
  const { encrypted, readable, fileSource, pdfSource } = useFilePreviewSources(
    fileId,
    fileType,
    sourceFile,
  );
  const sourceUrl = readable.url ?? undefined;
  const download = () => {
    void onDownload(fileId, fileName);
  };

  if (fileType === "other") {
    return (
      <FilePreviewUnavailable
        message={getUnavailablePreviewMessage(
          fileName,
          sourceFile?.contentType,
          fileSizeBytes,
          encrypted,
          t,
        )}
        onDownload={download}
      />
    );
  }

  if (encrypted && (readable.failed || !readable.url)) {
    return (
      <Box
        sx={{
          display: "flex",
          flex: 1,
          minHeight: 0,
          alignItems: "center",
          justifyContent: "center",
        }}
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
          src={sourceUrl}
          alt={fileName}
          onError={readable.onSourceError}
          sx={{
            width: "100%",
            height: "100%",
            minHeight: 0,
            objectFit: "contain",
          }}
        />
      );
    case "video":
      return (
        <Box
          component="video"
          src={sourceUrl}
          controls
          playsInline
          autoPlay
          preload="metadata"
          onError={readable.onSourceError}
          sx={{
            width: "100%",
            height: "100%",
            minHeight: 0,
            objectFit: "contain",
          }}
        />
      );
    case "audio":
      return (
        <Box
          sx={{
            display: "flex",
            flex: 1,
            alignItems: "center",
            px: 3,
          }}
        >
          <AudioPreview
            nodeFileId={fileId}
            fileName={fileName}
            sourceUrl={sourceUrl}
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
          previewUrl={sourceUrl}
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
  <Box
    sx={{
      display: "flex",
      flex: 1,
      flexDirection: "column",
      minHeight: 0,
    }}
  >
    <Box
      sx={{
        flex: 1,
        minHeight: 0,
      }}
    >
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

function getUnavailablePreviewMessage(
  fileName: string,
  contentType: string | null | undefined,
  fileSizeBytes: number | null,
  encrypted: boolean,
  t: TFunction<"files">,
): string | undefined {
  const originalType = getFileTypeInfo(fileName, contentType).type;
  let limit: number | null = null;
  if (encrypted) {
    limit = CLIENT_ENCRYPTION_BLOB_PIPELINE_MAX_BYTES;
    if (originalType === "pdf") {
      limit = previewConfig.MAX_PDF_PREVIEW_SIZE_BYTES;
    }
  } else if (originalType === "text") {
    limit = previewConfig.MAX_TEXT_PREVIEW_SIZE_BYTES;
  }
  const size = fileSizeBytes ?? 0;
  if (limit !== null && size > limit) {
    return t("preview.errors.fileTooLarge", {
      size: formatBytes(size),
      maxSize: formatBytes(limit),
    });
  }
  return undefined;
}
