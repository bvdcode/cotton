export const shareLinks = {
  /**
   * Builds a public app URL that can be opened without auth.
   *
   * NOTE: `token` is expected to be opaque and URL-safe.
   */
  buildShareUrl: (token: string, origin?: string): string => {
    const base =
      origin ?? (typeof window !== "undefined" ? window.location.origin : "");
    return `${base}/s/${encodeURIComponent(token)}`;
  },

  /**
   * Tries to extract an opaque token from the download URL returned by backend.
   * Supports several formats to be resilient to backend changes.
   */
  tryExtractTokenFromDownloadUrl: (downloadUrl: string): string | null => {
    if (!downloadUrl || downloadUrl.trim().length === 0) return null;

    try {
      const url = new URL(
        downloadUrl,
        typeof window !== "undefined" ? window.location.origin : "http://localhost",
      );

      const qpToken = url.searchParams.get("token");
      if (qpToken && qpToken.length > 0) return qpToken;

      const segments = url.pathname.split("/").filter(Boolean);
      if (segments.length === 0) return null;

      const downloadIndex = segments.findIndex((s) => {
        const lower = s.toLowerCase();
        return lower === "download" || lower === "d";
      });
      if (downloadIndex >= 0 && segments[downloadIndex + 1]) {
        return decodeURIComponent(segments[downloadIndex + 1]);
      }

      return decodeURIComponent(segments[segments.length - 1] ?? "");
    } catch {
      const normalized = downloadUrl.split("?")[0] ?? "";
      const parts = normalized.split("/").filter(Boolean);
      const last = parts[parts.length - 1];
      return last ? decodeURIComponent(last) : null;
    }
  },

  /**
   * Candidate backend endpoints for serving a file by share token.
   * SharePage will probe these patterns and use the first one that works.
   */
  buildTokenDownloadUrlCandidates: (token: string): string[] => {
    const encoded = encodeURIComponent(token);
    return [
      // Backend public share endpoint (preferred)
      `/s/${encoded}`,

      // Legacy/alternate endpoints (keep as fallbacks)
      `/api/v1/files/download/${encoded}`,
      `/api/v1/files/download?token=${encoded}`,
    ];
  },
} as const;
