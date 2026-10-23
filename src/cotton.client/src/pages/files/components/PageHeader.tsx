import React from "react";
import {
  Box,
  Fade,
  Divider,
  IconButton,
  Menu,
  MenuItem,
  Tooltip,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import { MoreVert, ViewModule, ViewList } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { FileBreadcrumbs } from "./FileBreadcrumbs";
import {
  getNextFileBrowserViewTitleKey,
  getTilesIconScale,
} from "@shared/utils/viewMode";
import { useOverflowActionKeys } from "../hooks/useOverflowActionKeys";
import { useTransitionActionItems } from "../hooks/useTransitionActionItems";
import { useLongPress } from "@shared/hooks/useLongPress";
import { formatBytes } from "@shared/utils/formatBytes";

import type { PageHeaderActionItem, PageHeaderProps } from "./PageHeaderTypes";
export type { PageHeaderActionItem, PageHeaderProps } from "./PageHeaderTypes";

import { buildPageHeaderActions, buildStatsSummary } from "./PageHeaderActions";

export const PageHeader: React.FC<PageHeaderProps> = ({
  loading,
  breadcrumbs,
  onNavigateBreadcrumb,
  stats,
  isSizeCalculating = false,
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
  const theme = useTheme();
  const reducedMotion = useMediaQuery("(prefers-reduced-motion: reduce)");
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
  const showSize =
    stats.sizeBytes > 0 || stats.folders > 0 || isSizeCalculating;
  const showHeaderMeta =
    Boolean(statsSummary) || showSize || (selectionMode && selectedCount > 0);

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

  const visibleActions: PageHeaderActionItem[] = actionTabs.filter((action) =>
    visibleActionKeys.includes(action.key),
  );
  if (overflowActions.length > 0) {
    visibleActions.push({
      key: "overflow",
      icon: <MoreVert />,
      title: t("common:actions.more"),
      onClick: (event) => {
        if (event) {
          setMenuAnchorEl(event.currentTarget);
        }
      },
    });
  }
  const transitionDuration = reducedMotion
    ? 0
    : theme.transitions.duration.shorter;
  const transition = useTransitionActionItems(
    visibleActions,
    actionButtonRefs,
    transitionDuration,
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
            justifyContent: { xs: "space-between", md: "flex-start" },
            flexShrink: 0,
            minWidth: 0,
            gap: { xs: 0, md: 0.5 },
            order: { xs: 1, md: 1 },
            width: { xs: "100%", md: "auto" },
            overflow: "hidden",
          }}
        >
          {transition.actions.map((action) => {
            const isVisible = visibleActions.some(
              (item) => item.key === action.key,
            );
            return (
              <Fade
                key={action.key}
                in={isVisible}
                appear
                timeout={transitionDuration}
                onExited={() => transition.handleExited(action.key)}
              >
                <Box
                  component="span"
                  aria-hidden={!isVisible}
                  sx={{
                    display: "inline-flex",
                    flexShrink: 0,
                  }}
                >
                  <Tooltip
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
                        display: "inline-flex",
                      }}
                    >
                      <IconButton
                        ref={(el) => {
                          actionButtonRefs.current[action.key] = el;
                        }}
                        aria-label={action.title}
                        aria-pressed={action.active}
                        color={action.color ?? "primary"}
                        disabled={action.disabled || !isVisible}
                        onClick={action.onClick}
                        {...(action.key === "go-up"
                          ? goUpLongPressHandlers
                          : {})}
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
                </Box>
              </Fade>
            );
          })}
          {overflowActions.length > 0 && (
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
                display: "flex",
                alignItems: "center",
                gap: 1,
                flexShrink: 0,
                minWidth: 0,
              }}
            >
              {(statsSummary || showSize) && (
                <Typography
                  noWrap
                  sx={{
                    color: "text.secondary",
                    fontSize: "0.875rem",
                    whiteSpace: "nowrap",
                    overflow: "hidden",
                    textOverflow: "ellipsis",
                  }}
                >
                  <Box
                    component="span"
                    sx={{ display: { xs: "none", md: "inline" } }}
                  >
                    {statsSummary}
                    {statsSummary &&
                      showSize &&
                      t("stats.separator", { ns: statsNamespace })}
                  </Box>
                  {showSize && (
                    <Box
                      component="span"
                      aria-busy={isSizeCalculating}
                      title={
                        isSizeCalculating
                          ? t("files:folderSizes.calculating")
                          : undefined
                      }
                      sx={[
                        {
                          fontVariantNumeric: "tabular-nums",
                          color: "text.primary",
                          "--folder-size-pulse-opacity": 0.7,
                          animation:
                            isSizeCalculating && !reducedMotion
                              ? "folder-size-pulse 1s ease-in-out infinite"
                              : "none",
                          "@keyframes folder-size-pulse": {
                            "0%, 100%": { opacity: 1 },
                            "50%": {
                              opacity: "var(--folder-size-pulse-opacity)",
                            },
                          },
                        },
                        theme.applyStyles("dark", {
                          "--folder-size-pulse-opacity": 0.55,
                        }),
                      ]}
                    >
                      {formatBytes(stats.sizeBytes)}
                    </Box>
                  )}
                </Typography>
              )}

              {selectionMode && selectedCount > 0 && (
                <Typography
                  noWrap
                  sx={{
                    color: "text.secondary",
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
