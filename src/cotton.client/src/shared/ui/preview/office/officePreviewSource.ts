import { filesApi } from "../../../api/filesApi";
import { previewConfig } from "../../../config/previewConfig";

export type OfficePreviewSource =
  { kind: "fileId"; fileId: string } | { kind: "url"; url: string };

export async function loadOfficePreview(
  source: OfficePreviewSource,
  signal: AbortSignal,
): Promise<ArrayBuffer> {
  let url: string;
  switch (source.kind) {
    case "fileId":
      url = await filesApi.getDownloadLink(source.fileId);
      break;
    case "url":
      url = source.url;
      break;
  }
  signal.throwIfAborted();
  const response = await fetch(url, { signal });
  if (!response.ok || !response.body) {
    throw new Error(`Office download failed (${response.status}).`);
  }
  const reader = response.body.getReader();
  const chunks: Uint8Array<ArrayBuffer>[] = [];
  let length = 0;
  try {
    while (true) {
      const { value, done } = await reader.read();
      if (done) {
        break;
      }
      length += value.byteLength;
      if (length > previewConfig.MAX_OFFICE_PREVIEW_SIZE_BYTES) {
        throw new Error("Office preview exceeds the size limit.");
      }
      chunks.push(new Uint8Array(value));
    }
  } finally {
    await reader.cancel();
  }
  return new Blob(chunks).arrayBuffer();
}
