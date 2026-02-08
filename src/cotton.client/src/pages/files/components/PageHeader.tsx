import React from "react";
import type { ReactNode } from "react";
import { Box, Divider, IconButton, Typography } from "@mui/material";
import {
  ArrowUpward,
  CreateNewFolder,
  Home,
  UploadFile,
  ViewModule,
  ViewList,
} from "@mui/icons-material";
import { FileBreadcrumbs } from "./FileBreadcrumbs";
import { formatBytes } from "../../../shared/utils/formatBytes";
import type { FileBrowserViewMode } from "../hooks/useFilesLayout";

export interface PageHeaderProps {
  loading: boolean;
  breadcrumbs: Array<{ id: string; name: string }>;
  stats: { folders: number; files: number; sizeBytes: number };
  viewMode: FileBrowserViewMode;
  canGoUp: boolean;
  onGoUp: () => void;
  onHomeClick: () => void;
  onViewModeCycle: () => void;
  showViewModeToggle?: boolean;
  statsNamespace?: string;

  // Optional actions
  showUpload?: boolean;
  showNewFolder?: boolean;
  onUploadClick?: () => void;
  onNewFolderClick?: () => void;
  isCreatingFolder?: boolean;

  // Custom actions slot
  customActions?: ReactNode;

  // Translations
  t: (key: string, options?: Record<string, unknown>) => string;
}

/**
 * Shared sticky header for file/folder pages
 */
export const PageHeader: React.FC<PageHeaderProps> = ({
  loading,
  breadcrumbs,
  stats,
  viewMode,
  canGoUp,
  onGoUp,
  onHomeClick,
  onViewModeCycle,
  showViewModeToggle = true,
  statsNamespace = "files",
  showUpload = false,
  showNewFolder = false,
  onUploadClick,
  onNewFolderClick,
  isCreatingFolder = false,
  customActions,
  t,
}) => {
  const nextViewTitleKey: string = (() => {
    switch (viewMode) {
      case "table":
        return "actions.switchToSmallTilesView";
      case "tiles-small":
        return "actions.switchToMediumTilesView";
      case "tiles-medium":
        return "actions.switchToLargeTilesView";
      case "tiles-large":
        return "actions.switchToTableView";
      default:
        return "actions.switchToTableView";
    }
  })();

  const viewIcon =
    viewMode === "table" ? (
      <ViewList />
    ) : (
      <ViewModule
        sx={{
          transform:
            viewMode === "tiles-small"
              ? "scale(0.9)"
              : viewMode === "tiles-large"
                ? "scale(1.1)"
                : "scale(1)",
        }}
      />
    );

  return (
    <Box
      sx={{
        position: "sticky",
        top: 0,
        zIndex: 20,
        bgcolor: "background.default",
        display: "flex",
        flexDirection: "column",
        marginBottom: 2,
        borderBottom: 1,
        borderColor: "divider",
        paddingTop: 1,
        paddingBottom: 1,
      }}
    >
      <Box
        sx={{
          display: "flex",
          flexDirection: { xs: "row", sm: "row" },
          gap: { xs: 1, sm: 1 },
          alignItems: { xs: "stretch", sm: "center" },
        }}
      >
        <Box
          sx={{
            display: "flex",
            gap: 1,
            alignItems: "center",
            justifyContent: "space-between",
          }}
        >
          <Box
            sx={{
              display: "flex",
              gap: 0.5,
              alignItems: "center",
              flexShrink: 0,
            }}
          >
            <IconButton
              color="primary"
              onClick={onGoUp}
              disabled={loading || !canGoUp}
              title={t("actions.goUp")}
            >
              <ArrowUpward />
            </IconButton>
            {showUpload && onUploadClick && (
              <IconButton
                color="primary"
                onClick={onUploadClick}
                disabled={loading}
                title={t("actions.upload")}
              >
                <UploadFile />
              </IconButton>
            )}
            {showNewFolder && onNewFolderClick && (
              <IconButton
                color="primary"
                onClick={onNewFolderClick}
                disabled={loading || isCreatingFolder}
                title={t("actions.newFolder")}
              >
                <CreateNewFolder />
              </IconButton>
            )}
            {customActions}
            <IconButton
              onClick={onHomeClick}
              color="primary"
              title={t("breadcrumbs.root")}
            >
              <Home />
            </IconButton>
            {showViewModeToggle && (
              <IconButton
                color="primary"
                onClick={onViewModeCycle}
                title={t(nextViewTitleKey)}
              >
                {viewIcon}
              </IconButton>
            )}
          </Box>
        </Box>

        <FileBreadcrumbs breadcrumbs={breadcrumbs} />

        <Divider
          orientation="vertical"
          flexItem
          sx={{ mx: 1, display: { xs: "none", sm: "block" } }}
        />
        <Box
          sx={{
            flexShrink: 0,
            whiteSpace: "nowrap",
            display: { xs: "none", sm: "block" },
          }}
        >
          <Typography color="text.secondary" sx={{ fontSize: "0.875rem" }}>
            {t("stats.summary", {
              ns: statsNamespace,
              folders: stats.folders,
              files: stats.files,
              size: formatBytes(stats.sizeBytes),
            })}
          </Typography>
        </Box>
      </Box>
    </Box>
  );
};
