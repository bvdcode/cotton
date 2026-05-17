const RELOAD_TIMESTAMP_KEY = "cottonStaleChunkReloadAtMs";
const RELOAD_COOLDOWN_MS = 60_000;
let lastReloadAttemptMs = 0;

export function isStaleChunkError(error: unknown): boolean {
  const message =
    error instanceof Error
      ? error.message
      : typeof error === "string"
        ? error
        : "";

  if (!message) {
    return false;
  }

  return (
    message.includes("Failed to fetch dynamically imported module") ||
    message.includes("error loading dynamically imported module") ||
    message.includes("Importing a module script failed") ||
    message.includes("Loading chunk") ||
    message.includes("ChunkLoadError")
  );
}

export function maybeReloadForStaleChunk(error: unknown): boolean {
  if (!isStaleChunkError(error)) {
    return false;
  }

  const now = Date.now();
  if (now - lastReloadAttemptMs < RELOAD_COOLDOWN_MS) {
    return false;
  }

  try {
    const previous = sessionStorage.getItem(RELOAD_TIMESTAMP_KEY);
    if (previous !== null) {
      const previousMs = Number(previous);
      if (
        Number.isFinite(previousMs) &&
        now - previousMs < RELOAD_COOLDOWN_MS
      ) {
        return false;
      }
    }

    sessionStorage.setItem(RELOAD_TIMESTAMP_KEY, String(now));
  } catch {
    // Private browsing and sandboxed frames can block sessionStorage. Keep an
    // in-memory guard so repeated error events in the same tab do not cascade.
  }

  lastReloadAttemptMs = now;
  window.location.reload();
  return true;
}

export function installStaleChunkReloadHandler(): void {
  window.addEventListener("unhandledrejection", (event) => {
    if (maybeReloadForStaleChunk(event.reason)) {
      event.preventDefault();
    }
  });

  window.addEventListener("error", (event) => {
    if (maybeReloadForStaleChunk(event.error ?? event.message)) {
      event.preventDefault();
    }
  });
}
