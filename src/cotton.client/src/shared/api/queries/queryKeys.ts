const notificationsRoot = ["notifications"] as const;
const layoutsRoot = ["layouts"] as const;
const adminRoot = ["admin"] as const;

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
  admin: {
    all: () => adminRoot,
    users: {
      all: () => [...adminRoot, "users"] as const,
      list: (filters: { withStorage: boolean }) =>
        [...adminRoot, "users", "list", filters] as const,
    },
    gcTimeline: {
      all: () => [...adminRoot, "gcTimeline"] as const,
      detail: (params: {
        bucket: string;
        fromUtc?: string;
        toUtc?: string;
      }) => [...adminRoot, "gcTimeline", params] as const,
    },
    latestDbBackup: () => [...adminRoot, "latestDbBackup"] as const,
  },
} as const;
