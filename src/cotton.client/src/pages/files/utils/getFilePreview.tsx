import type { ReactNode } from "react";
import { getFileIcon } from "./icons";

/**
 * @deprecated Use getFileIcon from icons/index.ts instead
 * Get the preview image source or icon for a file
 * @param encryptedFilePreviewHashHex - The preview image id from the server
 * @param fileName - The file name to determine the extension
 * @returns Either a URL string for preview images or a Material-UI icon component
 */
export function getFilePreview(
  encryptedFilePreviewHashHex: string | null,
  fileName: string,
  contentType?: string | null,
): string | ReactNode {
  return getFileIcon(encryptedFilePreviewHashHex, fileName, contentType);
}
