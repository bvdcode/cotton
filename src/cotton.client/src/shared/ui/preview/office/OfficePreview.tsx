import { useEffect, useState, lazy, Suspense } from "react";
import { Alert, Box, CircularProgress } from "@mui/material";
import { useTranslation } from "react-i18next";
import { resolveOfficeFormat } from "../../../utils/officeFormats";
import { reportClientError } from "../../../utils/clientDiagnostics";
import { previewConfig } from "../../../config/previewConfig";
import { formatBytes } from "../../../utils/formatBytes";
import {
  loadOfficePreview,
  type OfficePreviewSource,
} from "./officePreviewSource";

const OfficeDocumentPreview = lazy(() => import("./OfficeDocumentPreview"));
const SpreadsheetPreview = lazy(() => import("./SpreadsheetPreview"));

interface OfficePreviewProps {
  source: OfficePreviewSource;
  fileName: string;
  contentType?: string | null;
  fileSizeBytes: number | null;
}

export function OfficePreview({
  source,
  fileName,
  contentType,
  fileSizeBytes,
}: OfficePreviewProps) {
  const { t } = useTranslation("files");
  const format = resolveOfficeFormat(fileName, contentType);
  const tooLarge =
    (fileSizeBytes ?? 0) > previewConfig.MAX_OFFICE_PREVIEW_SIZE_BYTES;
  const sourceKey = getSourceKey(source);
  const sourceKind = source.kind;
  const [result, setResult] = useState<{
    key: string;
    bytes?: ArrayBuffer;
    failed?: boolean;
  }>();
  const key = sourceKind + ":" + sourceKey;

  useEffect(() => {
    if (tooLarge || !format) {
      return;
    }
    const controller = new AbortController();
    const currentSource = createSource(sourceKind, sourceKey);
    void loadOfficePreview(currentSource, controller.signal)
      .then((bytes) => {
        if (!controller.signal.aborted) {
          setResult({ key, bytes });
        }
      })
      .catch((error) => {
        if (!controller.signal.aborted) {
          reportClientError("Failed to load Office preview", error);
          setResult({ key, failed: true });
        }
      });
    return () => controller.abort();
  }, [sourceKind, sourceKey, key, tooLarge, format]);

  if (tooLarge) {
    return (
      <Alert severity="info">
        {t("preview.errors.fileTooLarge", {
          size: formatBytes(fileSizeBytes ?? 0),
          maxSize: formatBytes(previewConfig.MAX_OFFICE_PREVIEW_SIZE_BYTES),
        })}
      </Alert>
    );
  }
  if (!format || (result?.key === key && result.failed)) {
    return (
      <Alert severity="error">
        {t("preview.errors.loadFailed", { error: "" })}
      </Alert>
    );
  }
  if (result?.key !== key || !result.bytes) {
    return (
      <Box
        sx={{
          display: "flex",
          flex: 1,
          alignItems: "center",
          justifyContent: "center",
        }}
      >
        <CircularProgress />
      </Box>
    );
  }
  switch (format) {
    case "spreadsheet":
      return (
        <Suspense fallback={<CircularProgress />}>
          <SpreadsheetPreview key={key} bytes={result.bytes} />
        </Suspense>
      );
    case "docx":
    case "pptx":
      return (
        <Suspense fallback={<CircularProgress />}>
          <OfficeDocumentPreview
            key={key}
            bytes={result.bytes}
            format={format}
            title={fileName}
          />
        </Suspense>
      );
  }
}

function getSourceKey(source: OfficePreviewSource): string {
  switch (source.kind) {
    case "fileId":
      return source.fileId;
    case "url":
      return source.url;
  }
}

function createSource(
  kind: OfficePreviewSource["kind"],
  key: string,
): OfficePreviewSource {
  switch (kind) {
    case "fileId":
      return { kind, fileId: key };
    case "url":
      return { kind, url: key };
  }
}
