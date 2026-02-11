import { create } from "zustand";
import { notificationsApi } from "../api/notificationsApi";
import type { NotificationDto } from "../types/notification";

const PAGE_SIZE = 20;

interface NotificationsState {
  notifications: NotificationDto[];
  unreadCount: number;
  page: number;
  hasMore: boolean;
  loading: boolean;
  loadingMore: boolean;

  fetchUnreadCount: () => Promise<void>;
  fetchFirstPage: () => Promise<void>;
  fetchNextPage: () => Promise<void>;

  markAsRead: (id: string) => Promise<void>;
  markAllAsRead: () => Promise<void>;

  prependNotification: (notification: NotificationDto) => void;
  reset: () => void;
}

export const useNotificationsStore = create<NotificationsState>()((set, get) => ({
  notifications: [],
  unreadCount: 0,
  page: 1,
  hasMore: true,
  loading: false,
  loadingMore: false,

  fetchUnreadCount: async () => {
    try {
      const count = await notificationsApi.getUnreadCount();
      set({ unreadCount: count });
    } catch {
      // silent
    }
  },

  fetchFirstPage: async () => {
    set({ loading: true });
    try {
      const result = await notificationsApi.list(1, PAGE_SIZE);
      set({
        notifications: result.data,
        page: 1,
        hasMore: result.data.length >= PAGE_SIZE,
      });
    } catch {
      // silent
    } finally {
      set({ loading: false });
    }
  },

  fetchNextPage: async () => {
    const { hasMore, loading, loadingMore, page } = get();
    if (!hasMore || loading || loadingMore) return;

    const nextPage = page + 1;
    set({ loadingMore: true });
    try {
      const result = await notificationsApi.list(nextPage, PAGE_SIZE);
      const existingIds = new Set(get().notifications.map((n) => n.id));
      const newItems = result.data.filter((n) => !existingIds.has(n.id));

      set((state) => ({
        notifications: [...state.notifications, ...newItems],
        page: nextPage,
        hasMore: result.data.length >= PAGE_SIZE,
      }));
    } catch {
      // silent
    } finally {
      set({ loadingMore: false });
    }
  },

  markAsRead: async (id: string) => {
    try {
      await notificationsApi.markAsRead(id);
      const now = new Date().toISOString();
      set((state) => ({
        notifications: state.notifications.map((n) =>
          n.id === id ? { ...n, readAt: now } : n,
        ),
        unreadCount: Math.max(0, state.unreadCount - 1),
      }));
    } catch {
      // silent
    }
  },

  markAllAsRead: async () => {
    try {
      await notificationsApi.markAllAsRead();
      const now = new Date().toISOString();
      set((state) => ({
        notifications: state.notifications.map((n) =>
          n.readAt ? n : { ...n, readAt: now },
        ),
        unreadCount: 0,
      }));
    } catch {
      // silent
    }
  },

  prependNotification: (notification: NotificationDto) => {
    set((state) => {
      const exists = state.notifications.some((n) => n.id === notification.id);
      if (exists) return state;
      return {
        notifications: [notification, ...state.notifications],
        unreadCount: notification.readAt
          ? state.unreadCount
          : state.unreadCount + 1,
      };
    });
  },

  reset: () => {
    set({
      notifications: [],
      unreadCount: 0,
      page: 1,
      hasMore: true,
      loading: false,
      loadingMore: false,
    });
  },
}));
