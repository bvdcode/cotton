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
} from "@shared/utils/viewMode";
import { useOverflowActionKeys } from "../hooks/useOverflowActionKeys";

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

type PageHeaderActionFactoryOptions = Pick<
  PageHeaderProps,
  | "canGoUp"
  | "customActionItems"
  | "goUpDropHandlers"
  | "isCreatingFolder"
  | "loading"
  | "onDeselectAll"
  | "onGoUp"
  | "onHomeClick"
  | "onNewFolderClick"
  | "onSelectAll"
  | "onToggleSelectionMode"
  | "onUploadClick"
  | "onViewModeCycle"
  | "selectedCount"
  | "selectionMode"
  | "showNewFolder"
  | "showUpload"
  | "showViewModeToggle"
> & {
  nextViewTitleKey: string;
  t: ReturnType<typeof useTranslation>["t"];
  viewIcon: ReactElement;
};

const buildPageHeaderActions = ({
  canGoUp,
  customActionItems,
  goUpDropHandlers,
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
}: PageHeaderActionFactoryOptions): PageHeaderActionItem[] => {
  const actions: PageHeaderActionItem[] = [
    createGoUpAction(canGoUp, loading, onGoUp, t, goUpDropHandlers),
  ];

  appendCreationActions(actions, {
    isCreatingFolder,
    loading,
    onNewFolderClick,
    onUploadClick,
    showNewFolder,
    showUpload,
    t,
  });
  actions.push(createHomeAction(onHomeClick, t));
  appendViewModeAction(actions, showViewModeToggle ?? true, onViewModeCycle, nextViewTitleKey, viewIcon, t);
  appendSelectionActions(actions, {
    onDeselectAll,
    onSelectAll,
    onToggleSelectionMode,
    selectedCount,
    selectionMode,
    t,
  });

  return customActionItems?.length ? [...actions, ...customActionItems] : actions;
};

const createGoUpAction = (
  canGoUp: boolean,
  loading: boolean,
  onGoUp: () => void,
  t: ReturnType<typeof useTranslation>["t"],
  goUpDropHandlers: PageHeaderProps["goUpDropHandlers"],
): PageHeaderActionItem => ({
  key: "go-up",
  icon: <ArrowUpward />,
  title: t("actions.goUp"),
  onClick: onGoUp,
  disabled: loading || !canGoUp,
  onDragOver: goUpDropHandlers?.onDragOver,
  onDragLeave: goUpDropHandlers?.onDragLeave,
  onDrop: goUpDropHandlers?.onDrop,
  dropActive: goUpDropHandlers?.active,
});

type CreationActionOptions = Pick<
  PageHeaderActionFactoryOptions,
  | "isCreatingFolder"
  | "loading"
  | "onNewFolderClick"
  | "onUploadClick"
  | "showNewFolder"
  | "showUpload"
  | "t"
>;

const appendCreationActions = (
  actions: PageHeaderActionItem[],
  options: CreationActionOptions,
) => {
  if (options.showUpload && options.onUploadClick) {
    actions.push({
      key: "upload",
      icon: <UploadFile />,
      title: options.t("actions.upload"),
      onClick: options.onUploadClick,
      disabled: options.loading,
    });
  }

  if (options.showNewFolder && options.onNewFolderClick) {
    actions.push({
      key: "new-folder",
      icon: <CreateNewFolder />,
      title: options.t("actions.newFolder"),
      onClick: options.onNewFolderClick,
      disabled: options.loading || options.isCreatingFolder,
    });
  }
};

const createHomeAction = (
  onHomeClick: () => void,
  t: ReturnType<typeof useTranslation>["t"],
): PageHeaderActionItem => ({
  key: "home",
  icon: <Home />,
  title: t("breadcrumbs.root"),
  onClick: onHomeClick,
  disabled: false,
});

const appendViewModeAction = (
  actions: PageHeaderActionItem[],
  showViewModeToggle: boolean,
  onViewModeCycle: () => void,
  nextViewTitleKey: string,
  viewIcon: ReactElement,
  t: ReturnType<typeof useTranslation>["t"],
) => {
  if (!showViewModeToggle) {
    return;
  }

  actions.push({
    key: "view-mode",
    icon: viewIcon,
    title: t(nextViewTitleKey),
    onClick: onViewModeCycle,
    disabled: false,
  });
};

type SelectionActionOptions = Pick<
  PageHeaderActionFactoryOptions,
  | "onDeselectAll"
  | "onSelectAll"
  | "onToggleSelectionMode"
  | "selectedCount"
  | "selectionMode"
  | "t"
>;

const appendSelectionActions = (
  actions: PageHeaderActionItem[],
  options: SelectionActionOptions,
) => {
  if (options.onToggleSelectionMode) {
    actions.push({
      key: "selection-mode",
      icon: options.selectionMode ? <CheckBox /> : <CheckBoxOutlineBlank />,
      title: options.t(options.selectionMode ? "selection.exit" : "selection.enter"),
      onClick: options.onToggleSelectionMode,
      disabled: false,
      active: options.selectionMode,
    });
  }

  if (options.selectionMode && options.onSelectAll) {
    actions.push({
      key: "select-all",
      icon: <SelectAll />,
      title: options.t("selection.selectAll"),
      onClick: options.onSelectAll,
      disabled: false,
    });
  }

  if (options.selectionMode && (options.selectedCount ?? 0) > 0 && options.onDeselectAll) {
    actions.push({
      key: "deselect-all",
      icon: <Deselect />,
      title: options.t("selection.deselectAll"),
      onClick: options.onDeselectAll,
      disabled: false,
    });
  }
};

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
  breadcrumbsDropHandlers,
  goUpDropHandlers,
}) => {
  const { t } = useTranslation(["files", "trash", "common"]);
  const nextViewTitleKey = getNextFileBrowserViewTitleKey(viewMode);
  const actionsContainerRef = React.useRef<HTMLDivElement | null>(null);
  const actionButtonRefs = React.useRef<Record<string, HTMLButtonElement | null>>({});
  const [menuAnchorEl, setMenuAnchorEl] = React.useState<HTMLElement | null>(null);

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
    () =>
      buildPageHeaderActions({
        canGoUp,
        customActionItems,
        goUpDropHandlers,
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
      }),
    [
      canGoUp,
      customActionItems,
      goUpDropHandlers,
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
    ],
  );

  const visibleActionKeys = useOverflowActionKeys({
    actions: actionTabs,
    actionsContainerRef,
    actionButtonRefs,
  });

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
                <Tooltip
                  key={action.key}
                  title={action.title}
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
                      disabled={action.disabled}
                      onClick={action.onClick}
                      onDragOver={action.onDragOver}
                      onDragLeave={action.onDragLeave}
                      onDrop={action.onDrop}
                      sx={(theme) => ({
                        color:
                          action.color === "error"
                            ? theme.palette.error.main
                            : action.active || action.color === "secondary"
                              ? theme.palette.secondary.main
                              : theme.palette.primary.main,
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
          }}
        >
          <FileBreadcrumbs
            breadcrumbs={breadcrumbs}
            onNavigateBreadcrumb={onNavigateBreadcrumb}
            dropHandlers={breadcrumbsDropHandlers}
          />

          <Divider
            orientation="vertical"
            flexItem
            sx={{ display: { xs: "none", sm: "block" } }}
          />

          <Box
            sx={{
              display: { xs: "none", md: "flex" },
              alignItems: "center",
              gap: 1,
              flexShrink: 0,
              minWidth: 0,
            }}
          >
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
        </Box>
      </Box>
    </Box>
  );
};
