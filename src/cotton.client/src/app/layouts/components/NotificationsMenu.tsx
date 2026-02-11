import {
  Badge,
  Box,
  Divider,
  IconButton,
  List,
  ListItem,
  ListItemText,
  Menu,
  Tooltip,
  Typography,
} from "@mui/material";
import {
  Notifications as NotificationsIcon,
  MarkChatRead,
} from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { useConfirm } from "material-ui-confirm";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useNotificationsStore } from "../../../shared/store/notificationsStore";
import { formatTimeAgo } from "../../../shared/utils/formatTimeAgo";

export const NotificationsMenu = () => {
  const { t } = useTranslation();
  const confirm = useConfirm();
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null);
  const open = Boolean(anchorEl);
  const listRef = useRef<HTMLUListElement | null>(null);
  const sentinelRef = useRef<HTMLLIElement | null>(null);

  const notifications = useNotificationsStore((s) =>
    Array.isArray(s.notifications) ? s.notifications : [],
  );
  const unreadCount = useNotificationsStore((s) => s.unreadCount);
  const hasMore = useNotificationsStore((s) => s.hasMore);
  const fetchFirstPage = useNotificationsStore((s) => s.fetchFirstPage);
  const fetchNextPage = useNotificationsStore((s) => s.fetchNextPage);
  const fetchUnreadCount = useNotificationsStore((s) => s.fetchUnreadCount);
  const markAsRead = useNotificationsStore((s) => s.markAsRead);
  const markAllAsRead = useNotificationsStore((s) => s.markAllAsRead);

  const handleOpen = useCallback(
    (e: React.MouseEvent<HTMLElement>) => {
      setAnchorEl(e.currentTarget);
      fetchFirstPage();
      fetchUnreadCount();
    },
    [fetchFirstPage, fetchUnreadCount],
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

  const handleMarkAllAsRead = useCallback(async () => {
    await markAllAsRead();
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
      maxHeight: 420,
      overflowY: "auto" as const,
    }),
    [],
  );

  const maxTextLength = 128;

  return (
    <>
      <Tooltip title={t("notifications.title")}>
        <IconButton size="small" onClick={handleOpen}>
          <Badge badgeContent={unreadCount} color="error">
            <NotificationsIcon fontSize="small" />
          </Badge>
        </IconButton>
      </Tooltip>

      <Menu
        anchorEl={anchorEl}
        open={open}
        onClose={handleClose}
        anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
        transformOrigin={{ vertical: "top", horizontal: "right" }}
        slotProps={{ paper: { sx: { width: 340 } } }}
      >
        <Box
          display="flex"
          alignItems="center"
          justifyContent="space-between"
          px={2}
          py={1}
        >
          <Typography variant="subtitle2" fontWeight={700}>
            {t("notifications.title")}
          </Typography>
          <Tooltip title={t("notifications.markAllAsRead")}>
            <IconButton size="small" onClick={handleMarkAllAsRead}>
              <MarkChatRead fontSize="small" />
            </IconButton>
          </Tooltip>
        </Box>

        <Divider />

        <List dense disablePadding ref={listRef} sx={menuListSx}>
          {notifications.length > 0 ? (
            notifications.map((n) => {
              const contentText =
                n.content && n.content.length > maxTextLength
                  ? n.content.slice(0, maxTextLength) + "..."
                  : n.content;

              return (
                <ListItem
                  key={n.id}
                  onMouseEnter={() => handleHover(n.id, n.readAt)}
                  onClick={async () => {
                    try {
                      const result = await confirm({
                        title: n.title,
                        description: n.content ?? undefined,
                        confirmationText: t("notifications.viewAll"),
                        cancellationText: t("common:actions.close"),
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
                          {n.title}
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
                      contentText ? (
                        <Typography variant="caption" color="text.secondary">
                          {contentText}
                        </Typography>
                      ) : null
                    }
                  />
                </ListItem>
              );
            })
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
