import { Folder } from "@mui/icons-material";
import type { IconResult } from "./types";

/**
 * Icon size constant - shared across all file system icons
 */
export const ICON_SIZE = 120;

/**
 * Get folder icon with theme-aware styling
 * 
 * Single Responsibility: Only handles folder icon rendering
 * Consistent sizing and theming for all folders
 */
export function getFolderIcon(): IconResult {
  return (
    <Folder
      sx={{
        fontSize: ICON_SIZE,
        color: (theme) =>
          theme.palette.mode === "light" 
            ? "rgba(0, 0, 0, 0.26)" 
            : "inherit",
      }}
    />
  );
}
