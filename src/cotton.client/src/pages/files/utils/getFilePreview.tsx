import { InsertDriveFile, Image, Folder } from "@mui/icons-material";
import { Box, Typography } from "@mui/material";
import type { ReactNode } from "react";

const ICON_FONT_SIZE = 120;

/**
 * Get folder icon with consistent sizing
 */
export function getFolderIcon(): ReactNode {
  return (
    <Folder 
      sx={{ 
        fontSize: ICON_FONT_SIZE,
        color: (theme) => theme.palette.mode === 'light' 
          ? 'rgba(0, 0, 0, 0.26)' 
          : 'inherit'
      }} 
    />
  );
}

/**
 * Get the preview image source or icon for a file
 * @param encryptedFilePreviewHashHex - The preview image id from the server
 * @param fileName - The file name to determine the extension
 * @returns Either a URL string for preview images or a Material-UI icon component
 */
export function getFilePreview(
  encryptedFilePreviewHashHex: string | null,
  fileName: string,
): string | ReactNode {
  // If preview hash is available, use the API endpoint
  if (encryptedFilePreviewHashHex) {
    return `/api/v1/preview/${encodeURIComponent(
      encryptedFilePreviewHashHex,
    )}.webp`;
  }

  // Fallback to icon based on file extension
  const extension = fileName.toLowerCase().split(".").pop() || "";

  // Map of extensions to icons
  const imageExtensions = ["jpg", "jpeg", "png", "gif", "webp", "bmp", "svg"];

  if (imageExtensions.includes(extension)) {
    return <Image sx={{ fontSize: ICON_FONT_SIZE }} />;
  }

  const maxExtensionLength = 6;
  const displayExtension =
    extension.length > maxExtensionLength
      ? extension.slice(0, maxExtensionLength)
      : extension;

  return (
    <Box sx={{ position: "relative", display: "inline-flex" }}>
      <InsertDriveFile
        sx={{
          fontSize: ICON_FONT_SIZE,
          color: (theme) => theme.palette.mode === 'light' 
            ? 'rgba(0, 0, 0, 0.26)' 
            : 'inherit'
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
          color: (theme) => theme.palette.mode === 'light'
            ? 'rgba(0, 0, 0, 0.6)'
            : 'text.secondary',
          pointerEvents: "none",
        }}
      >
        {displayExtension}
      </Typography>
    </Box>
  );
}
