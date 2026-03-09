import React from "react";
import type { ReactElement } from "react";
import {
  Box,
  Divider,
  IconButton,
  Menu,
  MenuItem,
  Typography,
} from "@mui/material";
import {
  ArrowUpward,
  CheckBox,
  CheckBoxOutlineBlank,
  CreateNewFolder,
  Deselect,
  Home,
  MoreVert,
  SelectAll,
  UploadFile,
  ViewModule,
  ViewList,
} from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { FileBreadcrumbs } from "./FileBreadcrumbs";
import { formatBytes } from "../../../shared/utils/formatBytes";
import {
  getNextFileBrowserViewTitleKey,
  getTilesIconScale,
  type FileBrowserViewMode,
} from "../utils/viewMode";

export interface PageHeaderActionItem {
  key: string;
  icon: ReactElement;
  title: string;
  onClick: () => void;
  disabled?: boolean;
  color?: "primary" | "secondary" | "error";
  active?: boolean;
}

export interface PageHeaderProps {
  loading: boolean;
  breadcrumbs: Array<{ id: string; name: string }>;
  onNavigateBreadcrumb?: (breadcrumbIndex: number) => void;
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

  // Selection mode
  selectionMode?: boolean;
  selectedCount?: number;
  onToggleSelectionMode?: () => void;
  onSelectAll?: () => void;
  onDeselectAll?: () => void;

  // Custom actions rendered in overflow-aware action bar
  customActionItems?: PageHeaderActionItem[];
}

/**
 * Shared sticky header for file/folder pages
 */
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
  showNewFolder = false,
  onUploadClick,
  onNewFolderClick,
  isCreatingFolder = false,
  selectionMode = false,
  selectedCount = 0,
  onToggleSelectionMode,
  onSelectAll,
  onDeselectAll,
  customActionItems,
}) => {
  const { t } = useTranslation(["files", "trash", "common"]);
  const nextViewTitleKey = getNextFileBrowserViewTitleKey(viewMode);
  const actionsContainerRef = React.useRef<HTMLDivElement | null>(null);
  const actionButtonRefs = React.useRef<Record<string, HTMLButtonElement | null>>({});
  const [menuAnchorEl, setMenuAnchorEl] = React.useState<HTMLElement | null>(null);
  const [visibleActionKeys, setVisibleActionKeys] = React.useState<string[]>([]);

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

  const actionTabs = React.useMemo(
    (): PageHeaderActionItem[] => {
      const actions: PageHeaderActionItem[] = [
        {
        key: "go-up",
        icon: <ArrowUpward />,
        title: t("actions.goUp"),
        onClick: onGoUp,
        disabled: loading || !canGoUp,
        },
      ];

      if (showUpload && onUploadClick) {
        actions.push({
          key: "upload",
          icon: <UploadFile />,
          title: t("actions.upload"),
          onClick: onUploadClick,
          disabled: loading,
        });
      }

      if (showNewFolder && onNewFolderClick) {
        actions.push({
          key: "new-folder",
          icon: <CreateNewFolder />,
          title: t("actions.newFolder"),
          onClick: onNewFolderClick,
          disabled: loading || isCreatingFolder,
        });
      }

      actions.push({
        key: "home",
        icon: <Home />,
        title: t("breadcrumbs.root"),
        onClick: onHomeClick,
        disabled: false,
      });

      if (showViewModeToggle) {
        actions.push({
          key: "view-mode",
          icon: viewIcon,
          title: t(nextViewTitleKey),
          onClick: onViewModeCycle,
          disabled: false,
        });
      }

      if (onToggleSelectionMode) {
        actions.push({
          key: "selection-mode",
          icon: selectionMode ? <CheckBox /> : <CheckBoxOutlineBlank />,
          title: t(selectionMode ? "selection.exit" : "selection.enter"),
          onClick: onToggleSelectionMode,
          disabled: false,
          active: selectionMode,
        });
      }

      if (selectionMode && onSelectAll) {
        actions.push({
          key: "select-all",
          icon: <SelectAll />,
          title: t("selection.selectAll"),
          onClick: onSelectAll,
          disabled: false,
        });
      }

      if (selectionMode && selectedCount > 0 && onDeselectAll) {
        actions.push({
          key: "deselect-all",
          icon: <Deselect />,
          title: t("selection.deselectAll"),
          onClick: onDeselectAll,
          disabled: false,
        });
      }

      if (customActionItems && customActionItems.length > 0) {
        actions.push(...customActionItems);
      }

      return actions;
    },
    [
      canGoUp,
      isCreatingFolder,
      loading,
      nextViewTitleKey,
      onDeselectAll,
      onGoUp,
      onHomeClick,
      onNewFolderClick,
      onSelectAll,
      onToggleSelectionMode,
      onUploadClick,
      onViewModeCycle,
      selectedCount,
      selectionMode,
      showNewFolder,
      showUpload,
      showViewModeToggle,
      t,
      viewIcon,
      customActionItems,
    ],
  );

  React.useLayoutEffect(() => {
    const container = actionsContainerRef.current;
    if (!container || actionTabs.length === 0) {
      setVisibleActionKeys([]);
      return;
    }

    const ACTION_GAP = 4;
    const MORE_BUTTON_WIDTH = 36;

    const measure = () => {
      const available = container.clientWidth;
      if (available <= 0) return;

      const widths = actionTabs.map((action) => {
        const el = actionButtonRefs.current[action.key];
        return el ? Math.ceil(el.getBoundingClientRect().width) : 36;
      });

      const totalWidth = widths.reduce((sum, w) => sum + w, 0) +
        Math.max(0, widths.length - 1) * ACTION_GAP;

      if (totalWidth <= available) {
        setVisibleActionKeys(actionTabs.map((a) => a.key));
        return;
      }

      const maxWithoutOverflow = Math.max(0, available - MORE_BUTTON_WIDTH - ACTION_GAP);
      const nextVisible: string[] = [];
      let consumed = 0;

      for (let i = 0; i < actionTabs.length; i += 1) {
        const width = widths[i] ?? 36;
        const projected = consumed + width + (nextVisible.length > 0 ? ACTION_GAP : 0);
        if (projected > maxWithoutOverflow) {
          break;
        }

        nextVisible.push(actionTabs[i].key);
        consumed = projected;
      }

      if (nextVisible.length === 0 && actionTabs.length > 0) {
        nextVisible.push(actionTabs[0].key);
      }

      setVisibleActionKeys(nextVisible);
    };

    measure();

    const observer = new ResizeObserver(() => measure());
    observer.observe(container);

    return () => {
      observer.disconnect();
    };
  }, [actionTabs]);

  const overflowActions = React.useMemo(
    () => actionTabs.filter((action) => !visibleActionKeys.includes(action.key)),
    [actionTabs, visibleActionKeys],
  );

  const closeMenu = React.useCallback(() => {
    setMenuAnchorEl(null);
  }, []);

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
          flexDirection: "row",
          alignItems: "center",
          gap: 1,
          minWidth: 0,
        }}
      >
        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            flexShrink: 0,
            maxWidth: { xs: "58%", md: "46%" },
            minWidth: 0,
            gap: 0.5,
          }}
        >
          <Box
            ref={actionsContainerRef}
            sx={{
              display: "flex",
              alignItems: "center",
              gap: 0.5,
              minWidth: 0,
              overflow: "hidden",
              flex: 1,
            }}
          >
            {actionTabs.map((action) => {
              const isVisible = visibleActionKeys.includes(action.key);
              return (
                <IconButton
                  key={action.key}
                  ref={(el) => {
                    actionButtonRefs.current[action.key] = el;
                  }}
                  aria-label={action.title}
                  title={action.title}
                  disabled={action.disabled}
                  onClick={action.onClick}
                  sx={{
                    display: isVisible ? "inline-flex" : "none",
                    color:
                      action.color === "error"
                        ? "error.main"
                        : action.active || action.color === "secondary"
                          ? "secondary.main"
                          : "primary.main",
                  }}
                >
                  {action.icon}
                </IconButton>
              );
            })}
          </Box>

          {overflowActions.length > 0 && (
            <>
              <IconButton
                aria-label={t("common:actions.more")}
                title={t("common:actions.more")}
                onClick={(e) => setMenuAnchorEl(e.currentTarget)}
                sx={{ color: "primary.main" }}
              >
                <MoreVert />
              </IconButton>
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
          }}
        >
          <FileBreadcrumbs
            breadcrumbs={breadcrumbs}
            onNavigateBreadcrumb={onNavigateBreadcrumb}
          />

          <Divider
            orientation="vertical"
            flexItem
            sx={{ display: { xs: "none", sm: "block" } }}
          />

          <Typography
            color="text.secondary"
            sx={{ fontSize: "0.875rem", display: { xs: "none", md: "block" } }}
          >
            {t("stats.summary", {
              ns: statsNamespace,
              folders: stats.folders,
              files: stats.files,
              size: formatBytes(stats.sizeBytes),
            })}
          </Typography>

          {selectionMode && selectedCount > 0 && (
            <Typography
              color="text.secondary"
              sx={{ fontSize: "0.875rem", display: { xs: "none", sm: "block" } }}
            >
              {t("selection.count", { count: selectedCount })}
            </Typography>
          )}
        </Box>
      </Box>
    </Box>
  );
};
