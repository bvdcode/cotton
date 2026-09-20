import { eventHub } from "./eventHub";
import { getHubMethodVariants, HUB_METHODS } from "./hubMethods";

type PreviewGeneratedHandler = (
  nodeId: string,
  nodeFileId: string,
  previewHashEncryptedHex: string,
) => void;

const PREVIEW_GENERATED_METHODS = getHubMethodVariants([
  HUB_METHODS.PreviewGenerated,
]);

export const subscribeToPreviewGenerated = (
  handler: PreviewGeneratedHandler,
): (() => void) => {
  const unsubscribes = PREVIEW_GENERATED_METHODS.map((method) =>
    eventHub.on(method, (nodeId, nodeFileId, previewHashEncryptedHex) => {
      if (
        typeof nodeId === "string" &&
        typeof nodeFileId === "string" &&
        typeof previewHashEncryptedHex === "string"
      ) {
        handler(nodeId, nodeFileId, previewHashEncryptedHex);
      }
    }),
  );

  return () => {
    for (const unsubscribe of unsubscribes) {
      unsubscribe();
    }
  };
};
