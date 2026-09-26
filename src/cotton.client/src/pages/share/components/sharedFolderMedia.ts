import type { SharedNodeFileDto } from "../../../shared/api/sharedFoldersApi";
import type { MediaItem } from "@shared/types/mediaLightbox";
import { getFileIcon } from "@shared/utils/icons";
import { getFileTypeInfo } from "@shared/utils/fileTypes";

export const buildSharedMediaItems = (
  sortedFiles: SharedNodeFileDto[],
): MediaItem[] =>
  sortedFiles
    .map((file) => ({
      file,
      typeInfo: getFileTypeInfo(file.name, file.contentType),
    }))
    .filter(
      ({ typeInfo }) => typeInfo.type === "image" || typeInfo.type === "video",
    )
    .map(({ file, typeInfo }) => {
      const preview = getFileIcon(
        file.previewHashEncryptedHex ?? null,
        file.name,
        file.contentType,
      );
      const previewUrl = typeof preview === "string" ? preview : "";

      return {
        id: file.id,
        kind: typeInfo.type === "image" ? "image" : "video",
        name: file.name,
        previewUrl,
        mimeType: file.contentType,
        sizeBytes: file.sizeBytes,
      };
    });
