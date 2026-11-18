import { eventHub } from "./eventHub";
import { HUB_METHODS } from "./hubMethods";

export type PreviewUpdates = ReadonlyMap<string, ReadonlyMap<string, string>>;
type PreviewGeneratedHandler = (previews: PreviewUpdates) => void;
export const PREVIEW_UPDATE_DELAY_MS = 250;

export const subscribeToPreviewGenerated = (
  handler: PreviewGeneratedHandler,
): (() => void) => {
  let pending = new Map<string, Map<string, string>>();
  let timer: ReturnType<typeof setTimeout> | null = null;
  const unsubscribe = eventHub.on(
    HUB_METHODS.PreviewGenerated,
    (nodeId, nodeFileId, previewHashEncryptedHex) => {
      if (
        typeof nodeId === "string" &&
        typeof nodeFileId === "string" &&
        typeof previewHashEncryptedHex === "string"
      ) {
        const files = pending.get(nodeId) ?? new Map<string, string>();
        files.set(nodeFileId, previewHashEncryptedHex);
        pending.set(nodeId, files);
        timer ??= setTimeout(() => {
          const previews = pending;
          pending = new Map();
          timer = null;
          handler(previews);
        }, PREVIEW_UPDATE_DELAY_MS);
      }
    },
  );
  return () => {
    unsubscribe();
    if (timer !== null) {
      clearTimeout(timer);
    }
    pending.clear();
  };
};
