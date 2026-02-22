import * as React from "react";
import { previewConfig } from "../../../shared/config/previewConfig";
import { tryParseFileName } from "../utils/tryParseFileName";

export type ShareFileInfoError = "invalidLink" | "notFound" | "loadFailed";

export interface ShareFileInfo {
  loading: boolean;
  error: ShareFileInfoError | null;
  fileName: string | null;
  contentType: string | null;
  contentLength: number | null;
  textContent: string | null;
  resolvedInlineUrl: string | null;
}

interface LoadedShareFileInfo {
  error: ShareFileInfoError | null;
  fileName: string | null;
  contentType: string | null;
  contentLength: number | null;
  textContent: string | null;
  resolvedInlineUrl: string | null;
}

async function fetchHeadOrGet(url: string): Promise<Response> {
  try {
    const head = await fetch(url, { method: "HEAD" });
    if (head.ok) return head;
    return await fetch(url, { method: "GET" });
  } catch {
    return await fetch(url, { method: "GET" });
  }
}

function looksLikeTextContent(contentType: string | null): boolean {
  const normalized = (contentType ?? "").toLowerCase();
  return (
    normalized.startsWith("text/") ||
    normalized.includes("json") ||
    normalized.includes("xml")
  );
}

async function tryResolveNameFromDownloadUrl(downloadUrl: string): Promise<string | null> {
  try {
    const resp = await fetch(downloadUrl, { method: "HEAD" });
    if (!resp.ok) return null;
    return tryParseFileName(resp.headers.get("content-disposition"));
  } catch {
    return null;
  }
}

async function loadShareFileInfo(args: {
  token: string;
  inlineUrl: string;
  downloadUrl: string | null;
}): Promise<LoadedShareFileInfo> {
  const { inlineUrl, downloadUrl } = args;

  try {
    const response = await fetchHeadOrGet(inlineUrl);
    if (!response.ok) {
      return {
        error: "notFound",
        fileName: null,
        contentType: null,
        contentLength: null,
        textContent: null,
        resolvedInlineUrl: null,
      };
    }

    const contentType = response.headers.get("content-type");
    const contentDisposition = response.headers.get("content-disposition");
    const contentLengthRaw = response.headers.get("content-length");

    const parsedLength = contentLengthRaw
      ? Number.parseInt(contentLengthRaw, 10)
      : Number.NaN;
    const contentLength = Number.isFinite(parsedLength) ? parsedLength : null;

    let fileName: string | null = tryParseFileName(contentDisposition);
    if (!fileName && downloadUrl) {
      fileName = await tryResolveNameFromDownloadUrl(downloadUrl);
    }

    let textContent: string | null = null;
    if (
      looksLikeTextContent(contentType) &&
      typeof contentLength === "number" &&
      contentLength <= previewConfig.MAX_SHARE_TEXT_PREVIEW_SIZE_BYTES
    ) {
      const textResp = await fetch(inlineUrl, { method: "GET" });
      if (!textResp.ok) {
        return {
          error: "loadFailed",
          fileName,
          contentType,
          contentLength,
          textContent: null,
          resolvedInlineUrl: inlineUrl,
        };
      }
      textContent = await textResp.text();
    }

    return {
      error: null,
      fileName,
      contentType,
      contentLength,
      textContent,
      resolvedInlineUrl: inlineUrl,
    };
  } catch {
    return {
      error: "loadFailed",
      fileName: null,
      contentType: null,
      contentLength: null,
      textContent: null,
      resolvedInlineUrl: null,
    };
  }
}

interface UseShareFileInfoArgs {
  token: string | null;
  inlineUrl: string | null;
  downloadUrl: string | null;
}

export function useShareFileInfo({
  token,
  inlineUrl,
  downloadUrl,
}: UseShareFileInfoArgs): ShareFileInfo {
  const [loading, setLoading] = React.useState<boolean>(true);
  const [error, setError] = React.useState<ShareFileInfoError | null>(null);

  const [fileName, setFileName] = React.useState<string | null>(null);
  const [contentType, setContentType] = React.useState<string | null>(null);
  const [contentLength, setContentLength] = React.useState<number | null>(null);
  const [textContent, setTextContent] = React.useState<string | null>(null);
  const [resolvedInlineUrl, setResolvedInlineUrl] = React.useState<string | null>(null);

  React.useEffect(() => {
    if (!token || !inlineUrl) {
      setLoading(false);
      setError("invalidLink");
      return;
    }

    let cancelled = false;

    setLoading(true);
    setError(null);
    setFileName(null);
    setContentType(null);
    setContentLength(null);
    setTextContent(null);
    setResolvedInlineUrl(null);

    void (async () => {
      const result = await loadShareFileInfo({ token, inlineUrl, downloadUrl });
      if (cancelled) return;

      setFileName(result.fileName);
      setContentType(result.contentType);
      setContentLength(result.contentLength);
      setTextContent(result.textContent);
      setResolvedInlineUrl(result.resolvedInlineUrl);
      setError(result.error);
      setLoading(false);
    })();

    return () => {
      cancelled = true;
    };
  }, [token, inlineUrl, downloadUrl]);

  return {
    loading,
    error,
    fileName,
    contentType,
    contentLength,
    textContent,
    resolvedInlineUrl,
  };
}
