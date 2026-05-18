import * as React from "react";
import { looksLikeContainer } from "../../../shared/crypto/container";
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
  encryptedContainer: boolean;
}

interface LoadedShareFileInfo {
  error: ShareFileInfoError | null;
  fileName: string | null;
  contentType: string | null;
  contentLength: number | null;
  textContent: string | null;
  resolvedInlineUrl: string | null;
  encryptedContainer: boolean;
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

function isOctetStream(contentType: string | null): boolean {
  return (
    (contentType ?? "").split(";")[0].trim().toLowerCase() ===
    "application/octet-stream"
  );
}

function looksLikeOpaqueServerFileName(fileName: string | null): boolean {
  return (
    fileName !== null &&
    /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(
      fileName,
    )
  );
}

function shouldSniffEncryptedContainer(
  fileName: string | null,
  contentType: string | null,
): boolean {
  return isOctetStream(contentType) && looksLikeOpaqueServerFileName(fileName);
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

async function sniffEncryptedContainer(
  url: string,
  contentLength: number | null,
): Promise<boolean> {
  if (typeof contentLength === "number" && contentLength < 4) {
    return false;
  }

  try {
    const response = await fetch(url, {
      method: "GET",
      headers: { Range: "bytes=0-3" },
    });

    if (response.status !== 206) {
      await response.body?.cancel();
      return false;
    }

    const bytes = new Uint8Array(await response.arrayBuffer());
    return looksLikeContainer(bytes);
  } catch {
    return false;
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
        encryptedContainer: false,
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

    const encryptedContainer = shouldSniffEncryptedContainer(fileName, contentType)
      ? await sniffEncryptedContainer(inlineUrl, contentLength)
      : false;

    let textContent: string | null = null;
    if (
      !encryptedContainer &&
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
          encryptedContainer,
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
      encryptedContainer,
    };
  } catch {
    return {
      error: "loadFailed",
      fileName: null,
      contentType: null,
      contentLength: null,
      textContent: null,
      resolvedInlineUrl: null,
      encryptedContainer: false,
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
  const [encryptedContainer, setEncryptedContainer] =
    React.useState<boolean>(false);

  React.useEffect(() => {
    if (!token || !inlineUrl) {
      setLoading(false);
      setError("invalidLink");
      setFileName(null);
      setContentType(null);
      setContentLength(null);
      setTextContent(null);
      setResolvedInlineUrl(null);
      setEncryptedContainer(false);
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
    setEncryptedContainer(false);

    void (async () => {
      const result = await loadShareFileInfo({ token, inlineUrl, downloadUrl });
      if (cancelled) return;

      setFileName(result.fileName);
      setContentType(result.contentType);
      setContentLength(result.contentLength);
      setTextContent(result.textContent);
      setResolvedInlineUrl(result.resolvedInlineUrl);
      setEncryptedContainer(result.encryptedContainer);
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
    encryptedContainer,
  };
}
