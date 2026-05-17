const notificationsRoot = ["notifications"] as const;

export const queryKeys = {
  notifications: {
    all: () => notificationsRoot,
    list: (filters: { unreadOnly: boolean }) =>
      [...notificationsRoot, "list", filters] as const,
    unreadCount: () => [...notificationsRoot, "unreadCount"] as const,
  },
} as const;
