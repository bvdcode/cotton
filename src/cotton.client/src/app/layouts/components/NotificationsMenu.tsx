import {
  Badge,
  Box,
  CircularProgress,
  Divider,
  IconButton,
  List,
  ListItem,
  ListItemText,
  Menu,
  Tooltip,
  Typography,
  useMediaQuery,
} from "@mui/material";
import {
  Notifications as NotificationsIcon,
  MarkChatRead,
  VolumeUp,
  VolumeOff,
  FilterList,
  FilterListOff,
} from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { useConfirm } from "material-ui-confirm";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useTheme } from "@mui/material/styles";
import {
  useMarkAllAsReadMutation,
  useMarkAsReadMutation,
  useNotificationsQuery,
  useUnreadCountQuery,
} from "../../../shared/api/queries/notifications";
import {
  selectNotificationSoundEnabled,
  selectNotificationsShowOnlyUnread,
  useUserPreferencesStore,
} from "../../../shared/store/userPreferencesStore";
import { renderNotificationText } from "../../../shared/notifications/renderNotification";
import { formatTimeAgo } from "../../../shared/utils/formatTimeAgo";

export const NotificationsMenu = () => {
  const { t } = useTranslation(["notifications", "common"]);
  const confirm = useConfirm();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null);
  const open = Boolean(anchorEl);
  const listRef = useRef<HTMLUListElement | null>(null);
  const sentinelRef = useRef<HTMLLIElement | null>(null);

  const soundEnabled = useUserPreferencesStore(selectNotificationSoundEnabled);
  const showOnlyUnread = useUserPreferencesStore(
    selectNotificationsShowOnlyUnread,
  );
  const setSoundEnabled = useUserPreferencesStore(
    (s) => s.setNotificationSoundEnabled,
  );
  const setShowOnlyUnread = useUserPreferencesStore(
    (s) => s.setNotificationsShowOnlyUnread,
  );
  const notificationsQuery = useNotificationsQuery({
    unreadOnly: showOnlyUnread,
  });
  const {
    data: notificationsData,
    fetchNextPage: fetchNextNotificationsPage,
    hasNextPage,
    isPending: notificationsPending,
    isFetchingNextPage,
    refetch: refetchNotifications,
  } = notificationsQuery;
  const {
    data: unreadCount = 0,
    refetch: refetchUnreadCount,
  } = useUnreadCountQuery();
  const { mutate: markAsRead } = useMarkAsReadMutation();
  const { mutate: markAllAsRead } = useMarkAllAsReadMutation();

  const notifications = useMemo(
    () => notificationsData?.pages.flatMap((page) => page.data) ?? [],
    [notificationsData],
  );
  const hasMore = hasNextPage;
  const fetchNextPage = useCallback(() => {
    if (hasNextPage && !isFetchingNextPage) {
      void fetchNextNotificationsPage();
    }
  }, [fetchNextNotificationsPage, hasNextPage, isFetchingNextPage]);

  const handleOpen = useCallback(
    (e: React.MouseEvent<HTMLElement>) => {
      setAnchorEl(e.currentTarget);
      void refetchNotifications();
      void refetchUnreadCount();
    },
    [refetchNotifications, refetchUnreadCount],
  );

  const handleClose = useCallback(() => {
    setAnchorEl(null);
  }, []);

  const handleHover = useCallback(
    (id: string, readAt: string | null) => {
      if (!readAt) {
        markAsRead(id);
      }
    },
    [markAsRead],
  );

  const handleMarkAllAsRead = useCallback(() => {
    markAllAsRead();
  }, [markAllAsRead]);

  useEffect(() => {
    if (!open) return;
    const root = listRef.current;
    const target = sentinelRef.current;
    if (!root || !target) return;

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((e) => e.isIntersecting)) {
          fetchNextPage();
        }
      },
      { root, rootMargin: "160px 0px", threshold: 0 },
    );

    observer.observe(target);
    return () => observer.disconnect();
  }, [open, fetchNextPage]);

  const menuListSx = useMemo(
    () => ({
      maxHeight: isMobile ? "calc(100dvh - 140px)" : 420,
      overflowY: "auto" as const,
    }),
    [isMobile],
  );

  const menuPaperSx = useMemo(
    () => ({
      width: isMobile ? "100dvw" : 340,
      maxWidth: isMobile ? "100dvw" : 340,
      borderRadius: isMobile ? 0 : undefined,
      left: isMobile ? "0 !important" : undefined,
      right: isMobile ? "0 !important" : undefined,
      mt: isMobile ? 0.5 : 0,
    }),
    [isMobile],
  );

  return (
    <>
      <Tooltip title={t("title")}>
        <IconButton onClick={handleOpen}>
          <Badge badgeContent={unreadCount} color="error">
            <NotificationsIcon />
          </Badge>
        </IconButton>
      </Tooltip>

      <Menu
        anchorEl={anchorEl}
        open={open}
        onClose={handleClose}
        anchorOrigin={
          isMobile
            ? { vertical: "bottom", horizontal: "left" }
            : { vertical: "bottom", horizontal: "right" }
        }
        transformOrigin={
          isMobile
            ? { vertical: "top", horizontal: "left" }
            : { vertical: "top", horizontal: "right" }
        }
        slotProps={{ paper: { elevation: 3, sx: menuPaperSx } }}
      >
        <Box
          display="flex"
          alignItems="center"
          justifyContent="space-between"
          px={2}
          py={1}
        >
          <Typography variant="subtitle2" fontWeight={700}>
            {t("title")}
          </Typography>
          <Box display="flex" gap={0.5}>
            <Tooltip
              title={showOnlyUnread ? t("showAll") : t("showOnlyUnread")}
            >
              <IconButton
                size="small"
                onClick={() => setShowOnlyUnread(!showOnlyUnread)}
                sx={{ color: "text.secondary" }}
              >
                {showOnlyUnread ? (
                  <FilterListOff fontSize="small" />
                ) : (
                  <FilterList fontSize="small" />
                )}
              </IconButton>
            </Tooltip>
            <Tooltip title={soundEnabled ? t("muteSound") : t("unmuteSound")}>
              <IconButton
                size="small"
                onClick={() => setSoundEnabled(!soundEnabled)}
                sx={{ color: "text.secondary" }}
              >
                {soundEnabled ? (
                  <VolumeUp fontSize="small" />
                ) : (
                  <VolumeOff fontSize="small" />
                )}
              </IconButton>
            </Tooltip>
            <Tooltip title={t("markAllAsRead")}>
              <IconButton
                size="small"
                onClick={handleMarkAllAsRead}
                sx={{ color: "text.secondary" }}
              >
                <MarkChatRead fontSize="small" />
              </IconButton>
            </Tooltip>
          </Box>
        </Box>

        <Divider />

        <List dense disablePadding ref={listRef} sx={menuListSx}>
          {notifications.length > 0 ? (
            notifications.map((n) => {
              const rendered = renderNotificationText(n, t);

              return (
                <ListItem
                  key={n.id}
                  onMouseEnter={() => handleHover(n.id, n.readAt)}
                  onClick={async () => {
                    try {
                      const result = await confirm({
                        title: rendered.title,
                        description: rendered.content ? (
                          <Typography
                            variant="body2"
                            color="text.secondary"
                            sx={{ whiteSpace: "pre-line" }}
                          >
                            {rendered.content}
                          </Typography>
                        ) : undefined,
                        hideCancelButton: true,
                        confirmationText: t("common:ok"),
                      });

                      if (result?.confirmed) {
                        markAsRead(n.id);
                      }
                    } catch (err) {
                      console.debug("notification confirm failed", err);
                    }
                  }}
                  sx={{
                    px: 2,
                    py: 1,
                    cursor: "pointer",
                    borderBottom: "1px solid",
                    borderColor: "divider",
                    "&:last-of-type": { borderBottom: 0 },
                    bgcolor: n.readAt ? "transparent" : "action.selected",
                    "&:hover": {
                      bgcolor: "action.hover",
                    },
                  }}
                >
                  <ListItemText
                    primary={
                      <Box
                        display="flex"
                        alignItems="center"
                        gap={1}
                        minWidth={0}
                      >
                        <Typography
                          variant="body2"
                          noWrap
                          fontWeight={n.readAt ? 400 : 600}
                        >
                          {rendered.title}
                        </Typography>
                        <Box flex={1} />
                        <Typography
                          variant="caption"
                          color="text.secondary"
                          noWrap
                        >
                          {formatTimeAgo(n.createdAt, t)}
                        </Typography>
                      </Box>
                    }
                    secondary={
                      rendered.content ? (
                        <Typography
                          variant="caption"
                          color="text.secondary"
                          sx={{
                            display: "-webkit-box",
                            WebkitLineClamp: 2,
                            WebkitBoxOrient: "vertical",
                            overflow: "hidden",
                          }}
                        >
                          {rendered.content}
                        </Typography>
                      ) : null
                    }
                  />
                </ListItem>
              );
            })
          ) : notificationsPending ? (
            <ListItem sx={{ px: 2, py: 1.5 }}>
              <Box
                display="flex"
                alignItems="center"
                justifyContent="center"
                width="100%"
              >
                <CircularProgress size={18} />
              </Box>
            </ListItem>
          ) : (
            <ListItem sx={{ px: 2 }}>
              <ListItemText
                primary={
                  <Typography variant="body2" fontStyle="italic">
                    {t("notifications.empty")}
                  </Typography>
                }
              />
            </ListItem>
          )}

          {open && notifications.length > 0 && hasMore && (
            <Box
              component="li"
              ref={sentinelRef}
              sx={{ listStyle: "none", height: 1 }}
            />
          )}
        </List>
      </Menu>
    </>
  );
};
