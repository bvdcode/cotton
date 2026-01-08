import { InsertDriveFile, Image } from "@mui/icons-material";
import type { ReactNode } from "react";

/**
 * Get the preview image source or icon for a file
 * @param previewImageHash - The preview image hash from the server
 * @param fileName - The file name to determine the extension
 * @returns Either a URL string for preview images or a Material-UI icon component
 */
export function getFilePreview(
  previewImageHash: string | null | undefined,
  fileName: string,
): string | ReactNode {
  // If preview hash is available, use the API endpoint
  if (previewImageHash) {
    return `/api/v1/preview/${previewImageHash}.webp`;
  }

  // Fallback to icon based on file extension
  const extension = fileName.toLowerCase().split(".").pop() || "";

  // Map of extensions to icons
  const imageExtensions = ["jpg", "jpeg", "png", "gif", "webp", "bmp", "svg"];

  if (imageExtensions.includes(extension)) {
    return <Image sx={{ fontSize: 56 }} />;
  }

  return <InsertDriveFile sx={{ fontSize: 56 }} />;
}
