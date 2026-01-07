/**
 * Get the preview image source for a file
 * @param previewImageHash - The preview image hash from the server
 * @param fileName - The file name to determine the extension
 * @returns The URL to the preview image
 */
export function getFilePreviewSrc(
  previewImageHash: string | null | undefined,
  fileName: string,
): string {
  // If preview hash is available, use the API endpoint
  if (previewImageHash) {
    return `/api/v1/preview/${previewImageHash}.webp`;
  }

  // Fallback to icon based on file extension
  const extension = fileName.toLowerCase().split(".").pop() || "";

  // Map of extensions to icons
  const iconMap: Record<string, string> = {
    jpg: "/assets/icons/image.webp",
    jpeg: "/assets/icons/image.webp",
    png: "/assets/icons/image.webp",
    gif: "/assets/icons/image.webp",
    webp: "/assets/icons/image.webp",
    bmp: "/assets/icons/image.webp",
    svg: "/assets/icons/image.webp",
  };

  return iconMap[extension] || "/assets/icons/file.svg";
}
