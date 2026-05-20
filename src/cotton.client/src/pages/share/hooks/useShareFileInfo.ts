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

type ShareFileInfoState = ShareFileInfo & {
  key: string;
};

const createInvalidShareFileInfo = (key: string): ShareFileInfoState => ({
  key,
  loading: false,
  error: "invalidLink",
  fileName: null,
  contentType: null,
  contentLength: null,
  textContent: null,
  resolvedInlineUrl: null,
  encryptedContainer: false,
});

const createLoadingShareFileInfo = (key: string): ShareFileInfoState => ({
  key,
  loading: true,
  error: null,
  fileName: null,
  contentType: null,
  contentLength: null,
  textContent: null,
  resolvedInlineUrl: null,
  encryptedContainer: false,
});

const buildShareFileInfoKey = (
  token: string | null,
  inlineUrl: string | null,
  downloadUrl: string | null,
): string => {
  return token && inlineUrl
    ? [token, inlineUrl, downloadUrl ?? ""].join("\u0000")
    : "";
};

export function useShareFileInfo({
  token,
  inlineUrl,
  downloadUrl,
}: UseShareFileInfoArgs): ShareFileInfo {
  const requestKey = React.useMemo(
    () => buildShareFileInfoKey(token, inlineUrl, downloadUrl),
    [downloadUrl, inlineUrl, token],
  );
  const [state, setState] = React.useState<ShareFileInfoState>(() =>
    requestKey
      ? createLoadingShareFileInfo(requestKey)
      : createInvalidShareFileInfo(requestKey),
  );
  const effectiveState = !requestKey
    ? createInvalidShareFileInfo(requestKey)
    : state.key === requestKey
      ? state
      : createLoadingShareFileInfo(requestKey);

  React.useEffect(() => {
    if (!token || !inlineUrl || !requestKey) {
      return;
    }

    let cancelled = false;

    void (async () => {
      const result = await loadShareFileInfo({ token, inlineUrl, downloadUrl });
      if (cancelled) return;

      setState({
        key: requestKey,
        loading: false,
        error: result.error,
        fileName: result.fileName,
        contentType: result.contentType,
        contentLength: result.contentLength,
        textContent: result.textContent,
        resolvedInlineUrl: result.resolvedInlineUrl,
        encryptedContainer: result.encryptedContainer,
      });
    })();

    return () => {
      cancelled = true;
    };
  }, [token, inlineUrl, downloadUrl, requestKey]);

  return {
    loading: effectiveState.loading,
    error: effectiveState.error,
    fileName: effectiveState.fileName,
    contentType: effectiveState.contentType,
    contentLength: effectiveState.contentLength,
    textContent: effectiveState.textContent,
    resolvedInlineUrl: effectiveState.resolvedInlineUrl,
    encryptedContainer: effectiveState.encryptedContainer,
  };
}
