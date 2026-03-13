import type { NodeFileManifestDto } from "../api/nodesApi";
import type { AudioPlaylistItem } from "../types/audio";
import { getFileTypeInfo } from "../../pages/files/utils/fileTypes";
import { getFileIcon } from "../../pages/files/utils/icons";

interface BuildAudioPlaylistOptions {
  fallbackNodeId?: string;
}

export const buildAudioPlaylistFromFiles = (
  files: ReadonlyArray<NodeFileManifestDto>,
  options?: BuildAudioPlaylistOptions,
): AudioPlaylistItem[] => {
  return files
    .filter((file) => getFileTypeInfo(file.name, file.contentType ?? null).type === "audio")
    .map((file) => {
      const previewToken =
        file.largeFilePreviewPresignedToken ??
        file.previewHashEncryptedHex ??
        null;

      const icon = getFileIcon(previewToken, file.name, file.contentType ?? null);

      return {
        id: file.id,
        name: file.name,
        nodeId: file.nodeId ?? options?.fallbackNodeId,
        previewUrl: typeof icon === "string" ? icon : undefined,
      };
    });
};
