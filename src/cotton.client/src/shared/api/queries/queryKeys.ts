const notificationsRoot = ["notifications"] as const;
const layoutsRoot = ["layouts"] as const;

export const queryKeys = {
  notifications: {
    all: () => notificationsRoot,
    list: (filters: { unreadOnly: boolean }) =>
      [...notificationsRoot, "list", filters] as const,
    unreadCount: () => [...notificationsRoot, "unreadCount"] as const,
  },
  layouts: {
    all: () => layoutsRoot,
    root: () => [...layoutsRoot, "root"] as const,
    stats: (layoutId: string) => [...layoutsRoot, "stats", layoutId] as const,
    recent: (layoutId: string, count: number) =>
      [...layoutsRoot, "recent", layoutId, count] as const,
  },
} as const;
