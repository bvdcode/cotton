import { eventHub } from "./eventHub";
import { HUB_METHODS } from "./hubMethods";

type PreviewGeneratedHandler = (
  nodeId: string,
  nodeFileId: string,
  previewHashEncryptedHex: string,
) => void;

export const subscribeToPreviewGenerated = (
  handler: PreviewGeneratedHandler,
): (() => void) =>
  eventHub.on(
    HUB_METHODS.PreviewGenerated,
    (nodeId, nodeFileId, previewHashEncryptedHex) => {
      if (
        typeof nodeId === "string" &&
        typeof nodeFileId === "string" &&
        typeof previewHashEncryptedHex === "string"
      ) {
        handler(nodeId, nodeFileId, previewHashEncryptedHex);
      }
    },
  );
