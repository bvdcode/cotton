import { InsertDriveFile, Image } from "@mui/icons-material";
import { Box, Typography } from "@mui/material";
import type { ReactNode } from "react";

/**
 * Get the preview image source or icon for a file
 * @param encryptedFilePreviewHash - The preview image id from the server
 * @param fileName - The file name to determine the extension
 * @returns Either a URL string for preview images or a Material-UI icon component
 */
export function getFilePreview(
  encryptedFilePreviewHash: string | null | undefined,
  fileName: string,
): string | ReactNode {
  // If preview hash is available, use the API endpoint
  if (encryptedFilePreviewHash) {
    return `/api/v1/preview/${encryptedFilePreviewHash}.webp`;
  }

  const iconFontSize = 120;

  // Fallback to icon based on file extension
  const extension = fileName.toLowerCase().split(".").pop() || "";

  // Map of extensions to icons
  const imageExtensions = ["jpg", "jpeg", "png", "gif", "webp", "bmp", "svg"];

  if (imageExtensions.includes(extension)) {
    return <Image sx={{ fontSize: iconFontSize }} />;
  }

  const maxExtensionLength = 6;
  const displayExtension =
    extension.length > maxExtensionLength
      ? extension.slice(0, maxExtensionLength)
      : extension;

  return (
    <Box sx={{ position: "relative", display: "inline-flex" }}>
      <InsertDriveFile sx={{ fontSize: iconFontSize }} />
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
          color: "text.secondary",
          pointerEvents: "none",
        }}
      >
        {displayExtension}
      </Typography>
    </Box>
  );
}
