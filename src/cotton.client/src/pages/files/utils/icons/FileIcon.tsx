import { Article, Image, InsertDriveFile, Inventory } from "@mui/icons-material";
import { Box, Typography } from "@mui/material";
import type { IconResult } from "./types";
import { ICON_SIZE } from "./FolderIcon";
import { getFileTypeInfo } from "../fileTypes";

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
  contentType?: string | null,
): IconResult {
  // Strategy 1: Use server-generated preview if available
  if (previewHash) {
    return getPreviewImageUrl(previewHash);
  }

  const extension = extractExtension(fileName);
  const fileType = getFileTypeInfo(fileName, contentType ?? undefined).type;

  if (fileType === "image") {
    return getImageFileIcon();
  }

  if (fileType === "archive") {
    return <Inventory sx={{ fontSize: ICON_SIZE }} />;
  }

  if (fileType === "document" || fileType === "text") {
    return <Article sx={{ fontSize: ICON_SIZE }} />;
  }

  // Fallback: Generic file icon with extension label
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
