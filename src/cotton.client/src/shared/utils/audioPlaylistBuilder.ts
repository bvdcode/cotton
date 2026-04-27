import type { NodeFileManifestDto } from "../api/nodesApi";
import type { AudioPlaylistItem } from "../types/audio";
import { getFileTypeInfo } from "../../pages/files/utils/fileTypes";
import { getFileIcon } from "../../pages/files/utils/icons";

export const buildAudioPlaylistFromFiles = (
  files: ReadonlyArray<NodeFileManifestDto>,
): AudioPlaylistItem[] => {
  return files
    .filter((file) => getFileTypeInfo(file.name, file.contentType ?? null).type === "audio")
    .map((file) => {
      const previewToken = file.previewHashEncryptedHex ?? null;

      const icon = getFileIcon(previewToken, file.name, file.contentType ?? null);

      return {
        id: file.id,
        name: file.name,
        nodeId: file.nodeId,
        previewUrl: typeof icon === "string" ? icon : undefined,
      };
    });
};
