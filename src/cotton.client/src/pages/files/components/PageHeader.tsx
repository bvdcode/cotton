import React from "react";
import type { ReactElement } from "react";
import {
  Box,
  Divider,
  IconButton,
  Menu,
  MenuItem,
  Tooltip,
  Typography,
} from "@mui/material";
import { MoreVert, ViewModule, ViewList } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { FileBreadcrumbs } from "./FileBreadcrumbs";
import {
  getNextFileBrowserViewTitleKey,
  getTilesIconScale,
  type FileBrowserViewMode,
} from "@shared/utils/viewMode";
import { useOverflowActionKeys } from "../hooks/useOverflowActionKeys";
import { useLongPress } from "@shared/hooks/useLongPress";
import type {
  FileBreadcrumb,
  FileListStats,
} from "../../../shared/types/FileListViewTypes";

export interface PageHeaderActionItem {
  key: string;
  icon: ReactElement;
  title: string;
  onClick: () => void;
  disabled?: boolean;
  color?: "primary" | "secondary" | "error";
  active?: boolean;
  /** Optional DnD drop target handlers (e.g. Up button accepts move drop). */
  onDragOver?: (event: React.DragEvent<HTMLElement>) => void;
  onDragLeave?: (event: React.DragEvent<HTMLElement>) => void;
  onDrop?: (event: React.DragEvent<HTMLElement>) => void;
  dropActive?: boolean;
}

export interface PageHeaderProps {
  loading: boolean;
  breadcrumbs: FileBreadcrumb[];
  onNavigateBreadcrumb?: (breadcrumbIndex: number) => void;
  stats: FileListStats;
  viewMode: FileBrowserViewMode;
  canGoUp: boolean;
  onGoUp: () => void;
  onHomeClick: () => void;
  onViewModeCycle: () => void;
  showViewModeToggle?: boolean;
  statsNamespace?: string;

  // Optional actions
  showUpload?: boolean;
  showNewFile?: boolean;
  showNewFolder?: boolean;
  onUploadClick?: () => void;
  onNewFileClick?: () => void;
  onNewFolderClick?: () => void;
  isCreatingFile?: boolean;
  isCreatingFolder?: boolean;

  // Selection mode
  selectionMode?: boolean;
  selectedCount?: number;
  onToggleSelectionMode?: () => void;
  onSelectAll?: () => void;
  onDeselectAll?: () => void;

  // Custom actions rendered in overflow-aware action bar
  customActionItems?: PageHeaderActionItem[];
  primaryActionItems?: PageHeaderActionItem[];

  /** Optional drop handlers for breadcrumbs (move drag target). */
  breadcrumbsDropHandlers?: React.ComponentProps<
    typeof FileBreadcrumbs
  >["dropHandlers"];
  /** Optional drop handlers attached to the "Go up" action. */
  goUpDropHandlers?: {
    onDragOver: (event: React.DragEvent<HTMLElement>) => void;
    onDragLeave: (event: React.DragEvent<HTMLElement>) => void;
    onDrop: (event: React.DragEvent<HTMLElement>) => void;
    active: boolean;
  };
}

import { buildPageHeaderActions, buildStatsSummary } from "./PageHeaderActions";

export const PageHeader: React.FC<PageHeaderProps> = ({
  loading,
  breadcrumbs,
  onNavigateBreadcrumb,
  stats,
  viewMode,
  canGoUp,
  onGoUp,
  onHomeClick,
  onViewModeCycle,
  showViewModeToggle = true,
  statsNamespace = "files",
  showUpload = false,
  showNewFile = false,
  showNewFolder = false,
  onUploadClick,
  onNewFileClick,
  onNewFolderClick,
  isCreatingFile = false,
  isCreatingFolder = false,
  selectionMode = false,
  selectedCount = 0,
  onToggleSelectionMode,
  onSelectAll,
  onDeselectAll,
  customActionItems,
  primaryActionItems,
  breadcrumbsDropHandlers,
  goUpDropHandlers,
}) => {
  const { t } = useTranslation(["files", "trash", "common"]);
  const nextViewTitleKey = getNextFileBrowserViewTitleKey(viewMode);
  const actionsContainerRef = React.useRef<HTMLDivElement | null>(null);
  const goUpLongPressHandlers = useLongPress(onHomeClick, loading || !canGoUp);
  const actionButtonRefs = React.useRef<
    Record<string, HTMLButtonElement | null>
  >({});
  const [menuAnchorEl, setMenuAnchorEl] = React.useState<HTMLElement | null>(
    null,
  );
  const statsSummary = React.useMemo(
    () => buildStatsSummary(stats, statsNamespace, t),
    [stats, statsNamespace, t],
  );
  const showHeaderMeta =
    Boolean(statsSummary) || (selectionMode && selectedCount > 0);

  const viewIcon = React.useMemo(
    () =>
      viewMode === "table" ? (
        <ViewList />
      ) : (
        <ViewModule
          sx={{
            transform: `scale(${getTilesIconScale(viewMode)})`,
          }}
        />
      ),
    [viewMode],
  );

  const actionTabs = buildPageHeaderActions({
    canGoUp,
    customActionItems,
    primaryActionItems,
    goUpDropHandlers,
    isCreatingFile,
    isCreatingFolder,
    loading,
    nextViewTitleKey,
    onDeselectAll,
    onGoUp,
    onNewFileClick,
    onNewFolderClick,
    onSelectAll,
    onToggleSelectionMode,
    onUploadClick,
    onViewModeCycle,
    selectedCount,
    selectionMode,
    showNewFile,
    showNewFolder,
    showUpload,
    showViewModeToggle,
    t,
    viewIcon,
  });

  const visibleActionKeys = useOverflowActionKeys({
    actions: actionTabs,
    actionsContainerRef,
    actionButtonRefs,
  });

  const overflowActions = React.useMemo(
    () =>
      actionTabs.filter((action) => !visibleActionKeys.includes(action.key)),
    [actionTabs, visibleActionKeys],
  );

  const closeMenu = React.useCallback(() => {
    setMenuAnchorEl(null);
  }, [setMenuAnchorEl]);

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
          flexDirection: { xs: "column", md: "row" },
          alignItems: { xs: "stretch", md: "center" },
          gap: 1,
          minWidth: 0,
        }}
      >
        <Box
          ref={actionsContainerRef}
          sx={{
            display: "flex",
            alignItems: "center",
            flexShrink: 0,
            minWidth: 0,
            gap: { xs: 0, md: 0.5 },
            order: { xs: 1, md: 1 },
            width: { xs: "100%", md: "auto" },
          }}
        >
          <Box
            sx={{
              display: "flex",
              alignItems: "center",
              gap: { xs: 0, md: 0.5 },
              minWidth: 0,
              overflow: "hidden",
              flex: 1,
            }}
          >
            {actionTabs.map((action) => {
              const isVisible = visibleActionKeys.includes(action.key);
              return (
                <Tooltip
                  key={action.key}
                  title={
                    action.key === "go-up"
                      ? t("actions.goUpHoldHint")
                      : action.title
                  }
                  disableInteractive
                >
                  <Box
                    component="span"
                    sx={{
                      display: isVisible ? "inline-flex" : "none",
                    }}
                  >
                    <IconButton
                      ref={(el) => {
                        actionButtonRefs.current[action.key] = el;
                      }}
                      aria-label={action.title}
                      aria-pressed={action.active}
                      color={action.color ?? "primary"}
                      disabled={action.disabled}
                      onClick={action.onClick}
                      {...(action.key === "go-up" ? goUpLongPressHandlers : {})}
                      onDragOver={action.onDragOver}
                      onDragLeave={action.onDragLeave}
                      onDrop={action.onDrop}
                      sx={(theme) => ({
                        transition:
                          "box-shadow 120ms ease-out, background-color 120ms ease-out",
                        ...(action.dropActive && {
                          boxShadow: `inset 0 0 0 2px ${theme.palette.primary.main}`,
                          backgroundColor: theme.palette.action.selected,
                        }),
                      })}
                    >
                      {action.icon}
                    </IconButton>
                  </Box>
                </Tooltip>
              );
            })}
          </Box>

          {overflowActions.length > 0 && (
            <>
              <Tooltip title={t("common:actions.more")} disableInteractive>
                <IconButton
                  ref={(el) => {
                    actionButtonRefs.current.overflow = el;
                  }}
                  aria-label={t("common:actions.more")}
                  onClick={(e) => setMenuAnchorEl(e.currentTarget)}
                  sx={{ color: "primary.main" }}
                >
                  <MoreVert />
                </IconButton>
              </Tooltip>
              <Menu
                anchorEl={menuAnchorEl}
                open={Boolean(menuAnchorEl)}
                onClose={closeMenu}
                anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
                transformOrigin={{ vertical: "top", horizontal: "right" }}
              >
                {overflowActions.map((action) => (
                  <MenuItem
                    key={action.key}
                    disabled={action.disabled}
                    onClick={() => {
                      closeMenu();
                      action.onClick();
                    }}
                    sx={{ gap: 1 }}
                  >
                    {action.icon}
                    <Typography variant="body2">{action.title}</Typography>
                  </MenuItem>
                ))}
              </Menu>
            </>
          )}
        </Box>

        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            flex: 1,
            gap: 1,
            minWidth: 0,
            order: { xs: 2, md: 2 },
            minHeight: (theme) => theme.spacing(3),
          }}
        >
          <FileBreadcrumbs
            breadcrumbs={breadcrumbs}
            onNavigateBreadcrumb={onNavigateBreadcrumb}
            dropHandlers={breadcrumbsDropHandlers}
          />

          {showHeaderMeta && (
            <Divider
              orientation="vertical"
              flexItem
              sx={{ display: { xs: "none", md: "block" } }}
            />
          )}

          {showHeaderMeta && (
            <Box
              sx={{
                display: { xs: "none", md: "flex" },
                alignItems: "center",
                gap: 1,
                flexShrink: 0,
                minWidth: 0,
              }}
            >
              {statsSummary && (
                <Typography
                  color="text.secondary"
                  noWrap
                  sx={{
                    fontSize: "0.875rem",
                    whiteSpace: "nowrap",
                    overflow: "hidden",
                    textOverflow: "ellipsis",
                  }}
                >
                  {statsSummary}
                </Typography>
              )}

              {selectionMode && selectedCount > 0 && (
                <Typography
                  color="text.secondary"
                  noWrap
                  sx={{
                    fontSize: "0.875rem",
                    whiteSpace: "nowrap",
                    flexShrink: 0,
                  }}
                >
                  {t("selection.count", { count: selectedCount })}
                </Typography>
              )}
            </Box>
          )}
        </Box>
      </Box>
    </Box>
  );
};
