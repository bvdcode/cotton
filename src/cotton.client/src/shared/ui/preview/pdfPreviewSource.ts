import { filesApi } from "../../api/filesApi";

export type PdfPreviewSource =
  | { kind: "fileId"; fileId: string }
  | { kind: "objectUrl"; url: string }
  | {
      kind: "url";
      cacheKey: string;
      getPreviewUrl: () => Promise<string>;
    };

const blobUrlCache = new Map<string, string>();

export function getCachedPdfUrl(source: PdfPreviewSource): string | undefined {
  switch (source.kind) {
    case "fileId":
      return blobUrlCache.get(source.fileId);
    case "url":
      return blobUrlCache.get(source.cacheKey);
    case "objectUrl":
      return source.url;
  }
}

export function createPdfPreviewUrl(
  source: PdfPreviewSource,
  blob: Blob,
): string {
  switch (source.kind) {
    case "objectUrl":
      return source.url;
    case "fileId": {
      const url = URL.createObjectURL(blob);
      blobUrlCache.set(source.fileId, url);
      return url;
    }
    case "url": {
      const url = URL.createObjectURL(blob);
      blobUrlCache.set(source.cacheKey, url);
      return url;
    }
  }
}

export async function resolvePdfPreviewUrl(
  source: PdfPreviewSource,
): Promise<string> {
  switch (source.kind) {
    case "url":
      return source.getPreviewUrl();
    case "objectUrl":
      return source.url;
    case "fileId": {
      const downloadUrl = await filesApi.getDownloadLink(
        source.fileId,
        60 * 24,
      );
      const url = downloadUrl.startsWith("http")
        ? downloadUrl
        : `${window.location.origin}${downloadUrl}`;
      return url + (url.includes("?") ? "&" : "?") + "download=false";
    }
  }
}
