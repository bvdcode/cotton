export interface ShareLinkActionArgs {
  title: string;
  text: string;
  url: string;
}

export type ShareLinkActionOutcome =
  | { kind: "shared" }
  | { kind: "copied" }
  | { kind: "aborted" }
  | { kind: "error"; error: "copyFailed" };

function isAbortError(error: unknown): boolean {
  return error instanceof Error && error.name === "AbortError";
}

/**
 * Tries to share a link via the Web Share API (if available) and falls back to copying
 * the URL to clipboard.
 *
 * Clipboard fallback always copies URL only (no extra lines), to keep behavior consistent
 * and predictable across the app.
 */
export async function shareLinkAction(
  args: ShareLinkActionArgs,
): Promise<ShareLinkActionOutcome> {
  if (
    typeof navigator !== "undefined" &&
    typeof navigator.share === "function"
  ) {
    try {
      await navigator.share({
        title: args.title,
        text: args.text,
        url: args.url,
      });
      return { kind: "shared" };
    } catch (e) {
      if (isAbortError(e)) {
        return { kind: "aborted" };
      }
    }
  }

  try {
    await navigator.clipboard.writeText(args.url);
    return { kind: "copied" };
  } catch {
    return { kind: "error", error: "copyFailed" };
  }
}
