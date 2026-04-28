import { Article, Image, InsertDriveFile, Inventory, VpnKey } from "@mui/icons-material";
import { Box, Typography } from "@mui/material";
import type { IconResult } from "./types";
import { ICON_SIZE } from "./FolderIcon";
import { getFileTypeInfo } from "../fileTypes";

/**
 * Maximum length for extension display on icon
 */
const MAX_EXTENSION_LENGTH = 6;
const CREDENTIAL_FILE_EXTENSIONS = new Set<string>([
  "ppk",
  "key",
  "crt",
  "cer",
  "pem",
]);

interface FileIconOptions {
  extensionLabelMaxLength?: number;
  hideLongExtensionLabel?: boolean;
  hideInvalidExtensionLabel?: boolean;
  hideExtensionLabel?: boolean;
}

interface ExtensionLabelOptions {
  extensionLabelMaxLength: number;
  hideLongExtensionLabel: boolean;
  hideInvalidExtensionLabel: boolean;
  hideExtensionLabel: boolean;
}

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
  options?: FileIconOptions,
): IconResult {
  // Strategy 1: Use server-generated preview if available
  if (previewHash) {
    return getPreviewImageUrl(previewHash);
  }

  const extension = extractExtension(fileName);
  const fileType = getFileTypeInfo(fileName, contentType ?? undefined).type;

  if (isCredentialFileExtension(extension)) {
    return <VpnKey sx={{ fontSize: ICON_SIZE }} />;
  }

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
  return getGenericFileIcon(extension, {
    extensionLabelMaxLength:
      options?.extensionLabelMaxLength ?? MAX_EXTENSION_LENGTH,
    hideLongExtensionLabel: options?.hideLongExtensionLabel ?? false,
    hideInvalidExtensionLabel: options?.hideInvalidExtensionLabel ?? false,
    hideExtensionLabel: options?.hideExtensionLabel ?? false,
  });
}

/**
 * Extract file extension from filename
 * Single Responsibility: Extension extraction logic
 */
function extractExtension(fileName: string): string {
  return fileName.toLowerCase().split(".").pop() || "";
}

function isCredentialFileExtension(extension: string): boolean {
  return CREDENTIAL_FILE_EXTENSIONS.has(extension);
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
function getGenericFileIcon(
  extension: string,
  options: ExtensionLabelOptions,
): IconResult {
  const displayExtension = formatExtensionLabel(extension, options);

  return (
    <Box
      sx={{
        position: "relative",
        display: "inline-flex",
        width: "70%",
        height: "70%",
      }}
    >
      <InsertDriveFile
        sx={{
          width: "100%",
          height: "100%",
          color: (theme) =>
            theme.palette.mode === "light" 
              ? "rgba(0, 0, 0, 0.26)" 
              : "inherit",
        }}
      />
      {displayExtension && (
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
      )}
    </Box>
  );
}

/**
 * Format extension label depending on rendering rules
 * Single Responsibility: Extension formatting
 */
function formatExtensionLabel(
  extension: string,
  options: ExtensionLabelOptions,
): string | null {
  if (options.hideExtensionLabel) {
    return null;
  }

  const normalizedExtension = extension.trim().toLowerCase();

  if (normalizedExtension.length === 0) {
    return null;
  }

  if (
    options.hideInvalidExtensionLabel &&
    !/^[a-z0-9]+$/.test(normalizedExtension)
  ) {
    return null;
  }

  if (
    options.hideLongExtensionLabel &&
    normalizedExtension.length > options.extensionLabelMaxLength
  ) {
    return null;
  }

  if (normalizedExtension.length > options.extensionLabelMaxLength) {
    return normalizedExtension.slice(0, options.extensionLabelMaxLength);
  }

  return normalizedExtension;
}
