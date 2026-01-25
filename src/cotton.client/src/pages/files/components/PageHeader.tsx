import React from "react";
import type { ReactNode } from "react";
import {
  Box,
  Divider,
  IconButton,
  LinearProgress,
  Typography,
} from "@mui/material";
import {
  ArrowUpward,
  CreateNewFolder,
  Home,
  UploadFile,
  ViewModule,
  ViewList,
} from "@mui/icons-material";
import { FileBreadcrumbs } from "./FileBreadcrumbs";
import { InterfaceLayoutType } from "../../../shared/api/layoutsApi";
import { formatBytes } from "../utils/formatBytes";

export interface PageHeaderProps {
  loading: boolean;
  breadcrumbs: Array<{ id: string; name: string }>;
  stats: { folders: number; files: number; sizeBytes: number };
  layoutType: InterfaceLayoutType;
  canGoUp: boolean;
  onGoUp: () => void;
  onHomeClick: () => void;
  onLayoutToggle: (layoutType: InterfaceLayoutType) => void;
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
  layoutType,
  canGoUp,
  onGoUp,
  onHomeClick,
  onLayoutToggle,
  statsNamespace = "files",
  showUpload = false,
  showNewFolder = false,
  onUploadClick,
  onNewFolderClick,
  isCreatingFolder = false,
  customActions,
  t,
}) => {
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
      {loading && (
        <LinearProgress
          sx={{
            position: "absolute",
            top: 0,
            left: 0,
            right: 0,
          }}
        />
      )}
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
            {layoutType === InterfaceLayoutType.Tiles ? (
              <IconButton
                color="primary"
                onClick={() => onLayoutToggle(InterfaceLayoutType.List)}
                title={t("actions.switchToListView")}
              >
                <ViewList />
              </IconButton>
            ) : (
              <IconButton
                color="primary"
                onClick={() => onLayoutToggle(InterfaceLayoutType.Tiles)}
                title={t("actions.switchToTilesView")}
              >
                <ViewModule />
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
