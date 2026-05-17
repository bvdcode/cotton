export const queryKeys = {
  notifications: {
    list: (filters: { unreadOnly: boolean }) =>
      ["notifications", "list", filters] as const,
    unreadCount: () => ["notifications", "unreadCount"] as const,
  },
} as const;
