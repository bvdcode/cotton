import { useMemo } from "react";
import type { NodeFileManifestDto } from "../../../api/nodesApi";
import { isFileEncrypted } from "../../../crypto";
import type { FileType } from "../../../utils/fileTypes";
import type { ModelPreviewSource } from "../modelPreviewTypes";
import { useReadablePreviewSource } from "./useReadablePreviewSource";

export function useFilePreviewSources(
  fileId: string,
  fileType: FileType | null,
  sourceFile?: NodeFileManifestDto | null,
) {
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

  return { encrypted, readable, fileSource, pdfSource };
}
