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

    const load = async () => {
      setLoading(true);
      setError(null);
      setFileName(null);
      setContentType(null);
      setContentLength(null);
      setTextContent(null);
      setResolvedInlineUrl(null);

      try {
        let response: Response;
        try {
          response = await fetch(inlineUrl, { method: "HEAD" });
          if (!response.ok) {
            response = await fetch(inlineUrl, { method: "GET" });
          }
        } catch {
          response = await fetch(inlineUrl, { method: "GET" });
        }

        if (cancelled) return;

        if (!response.ok) {
          setError("notFound");
          setLoading(false);
          return;
        }

        setResolvedInlineUrl(inlineUrl);

        const ct = response.headers.get("content-type");
        const cd = response.headers.get("content-disposition");
        const cl = response.headers.get("content-length");

        setContentType(ct);

        let resolvedName: string | null = tryParseFileName(cd);
        if (!resolvedName && downloadUrl) {
          try {
            const nameResp = await fetch(downloadUrl, { method: "HEAD" });
            if (nameResp.ok) {
              resolvedName = tryParseFileName(
                nameResp.headers.get("content-disposition"),
              );
            }
          } catch {
            // ignore
          }
        }

        if (cancelled) return;

        setFileName(resolvedName);

        const parsedLength = cl ? Number.parseInt(cl, 10) : Number.NaN;
        const nextLength = Number.isFinite(parsedLength) ? parsedLength : null;
        setContentLength(nextLength);

        // Lightweight text preview for small files only.
        const normalizedCt = (ct ?? "").toLowerCase();
        const looksLikeText =
          normalizedCt.startsWith("text/") ||
          normalizedCt.includes("json") ||
          normalizedCt.includes("xml");

        if (
          looksLikeText &&
          typeof nextLength === "number" &&
          nextLength <= previewConfig.MAX_SHARE_TEXT_PREVIEW_SIZE_BYTES
        ) {
          const textResp = await fetch(inlineUrl, { method: "GET" });
          if (!textResp.ok) {
            throw new Error("text download failed");
          }
          const text = await textResp.text();
          if (cancelled) return;
          setTextContent(text);
        }

        setLoading(false);
      } catch {
        if (cancelled) return;
        setError("loadFailed");
        setLoading(false);
      }
    };

    void load();

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
