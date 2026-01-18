import { InsertDriveFile, Image } from "@mui/icons-material";
import { Box, Typography } from "@mui/material";
import type { IconResult } from "./types";
import { ICON_SIZE } from "./FolderIcon";

/**
 * Configuration for file type detection
 * Open/Closed Principle: Easy to add new file types
 */
const FILE_TYPE_CONFIGS = {
  image: ["jpg", "jpeg", "png", "gif", "webp", "bmp", "svg"] as const,
  // Can be extended with video, document, etc.
} as const;

type ImageExtension = (typeof FILE_TYPE_CONFIGS.image)[number];

/**
 * Maximum length for extension display on icon
 */
const MAX_EXTENSION_LENGTH = 6;

/**
 * Get file icon based on preview availability and file extension
 * 
 * Single Responsibility: Only handles file icon rendering logic
 * 
 * @param previewHash - Optional preview image hash from server
 * @param fileName - File name to extract extension from
 * @returns Icon as URL string or React component
 */
export function getFileIcon(
  previewHash: string | null,
  fileName: string,
): IconResult {
  // Strategy 1: Use server-generated preview if available
  if (previewHash) {
    return getPreviewImageUrl(previewHash);
  }

  const extension = extractExtension(fileName);

  // Strategy 2: Use specialized icon for known file types
  if (isImageExtension(extension)) {
    return getImageFileIcon();
  }

  // Strategy 3: Generic file icon with extension label
  return getGenericFileIcon(extension);
}

/**
 * Extract file extension from filename
 * Single Responsibility: Extension extraction logic
 */
function extractExtension(fileName: string): string {
  return fileName.toLowerCase().split(".").pop() || "";
}

/**
 * Check if extension is an image type
 * Open/Closed: Easy to modify image extensions list
 */
function isImageExtension(extension: string): extension is ImageExtension {
  return FILE_TYPE_CONFIGS.image.includes(extension as ImageExtension);
}

/**
 * Generate preview image URL
 */
function getPreviewImageUrl(previewHash: string): string {
  return `/api/v1/preview/${encodeURIComponent(previewHash)}.webp`;
}

/**
 * Get icon for image files
 */
function getImageFileIcon(): IconResult {
  return <Image sx={{ fontSize: ICON_SIZE }} />;
}

/**
 * Get generic file icon with extension label overlay
 * 
 * Single Responsibility: Renders generic file icon with extension text
 */
function getGenericFileIcon(extension: string): IconResult {
  const displayExtension = truncateExtension(extension);

  return (
    <Box sx={{ position: "relative", display: "inline-flex" }}>
      <InsertDriveFile
        sx={{
          fontSize: ICON_SIZE,
          color: (theme) =>
            theme.palette.mode === "light" 
              ? "rgba(0, 0, 0, 0.26)" 
              : "inherit",
        }}
      />
      <Typography
        variant="caption"
        sx={{
          position: "absolute",
          top: "54%",
          left: "50%",
          transform: "translate(-50%, -50%)",
          fontWeight: 700,
          fontSize: 14,
          textTransform: "uppercase",
          color: (theme) =>
            theme.palette.mode === "light"
              ? "rgba(0, 0, 0, 0.6)"
              : "text.secondary",
          pointerEvents: "none",
        }}
      >
        {displayExtension}
      </Typography>
    </Box>
  );
}

/**
 * Truncate extension if too long
 * Single Responsibility: Extension formatting
 */
function truncateExtension(extension: string): string {
  return extension.length > MAX_EXTENSION_LENGTH
    ? extension.slice(0, MAX_EXTENSION_LENGTH)
    : extension;
}
