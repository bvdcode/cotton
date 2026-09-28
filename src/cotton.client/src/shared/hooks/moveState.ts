import type { NodeDto } from "../api/layoutsApi";
import { nodesApi } from "../api/nodesApi";
import { isAxiosError } from "../api/httpClient";
import {
  getFolderEncryptionPolicyState,
  isFileEncrypted,
} from "../crypto";
import type { MoveClipboardItem } from "../store/moveClipboardStore";
import { isJsonObject, type JsonValue } from "../types/json";

export const extractErrorMessage = <T>(error: T): string | null => {
  if (!isAxiosError<JsonValue>(error)) return null;
  const data = error.response?.data;
  if (data !== undefined && isJsonObject(data)) {
    if (typeof data.message === "string" && data.message.length > 0) {
      return data.message;
    }
  }
  return null;
};

export const getMoveTarget = async (nodeId: string): Promise<{
  node: NodeDto;
  encryptsNewFiles: boolean;
}> => {
  const [node, ancestors] = await Promise.all([
    nodesApi.getNode(nodeId),
    nodesApi.getAncestors(nodeId),
  ]);
  return {
    node,
    encryptsNewFiles: getFolderEncryptionPolicyState(node, ancestors).effectiveEnabled,
  };
};

export const needsEncryptionAfterMove = (item: MoveClipboardItem): boolean =>
  item.kind === "file" &&
  item.file !== undefined &&
  !isFileEncrypted(item.file.metadata);

export const needsDecryptionAfterMove = (item: MoveClipboardItem): boolean =>
  item.kind === "file" &&
  item.file !== undefined &&
  isFileEncrypted(item.file.metadata);
