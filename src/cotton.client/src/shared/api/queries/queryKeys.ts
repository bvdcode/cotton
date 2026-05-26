const notificationsRoot = ["notifications"] as const;
const layoutsRoot = ["layouts"] as const;
const adminRoot = ["admin"] as const;
const audioRoot = ["audio"] as const;
const trashRoot = ["trash"] as const;
const serverSettingsRoot = ["serverSettings"] as const;
const storageQuotaRoot = ["storageQuota"] as const;
const fileVersionsRoot = ["fileVersions"] as const;
const oidcRoot = ["oidc"] as const;

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
    securityDiagnostics: () => [...adminRoot, "securityDiagnostics"] as const,
  },
  audio: {
    all: () => audioRoot,
    trackLyrics: (params: { folderNodeId: string; trackName: string }) =>
      [...audioRoot, "trackLyrics", params] as const,
  },
  trash: {
    all: () => trashRoot,
    root: () => [...trashRoot, "root"] as const,
    meta: (nodeId: string) => [...trashRoot, "meta", nodeId] as const,
    children: {
      all: (nodeId: string) => [...trashRoot, "children", nodeId] as const,
      page: (
        nodeId: string,
        params: { page: number; pageSize: number; depth: number },
      ) => [...trashRoot, "children", nodeId, params] as const,
    },
  },
  serverSettings: {
    all: () => serverSettingsRoot,
  },
  storageQuota: {
    all: () => storageQuotaRoot,
    current: () => [...storageQuotaRoot, "current"] as const,
  },
  fileVersions: {
    all: () => fileVersionsRoot,
    list: (fileId: string) => [...fileVersionsRoot, "list", fileId] as const,
  },
  oidc: {
    all: () => oidcRoot,
    publicProviders: () => [...oidcRoot, "providers", "public"] as const,
    adminProviders: () => [...oidcRoot, "providers", "admin"] as const,
    links: () => [...oidcRoot, "links"] as const,
  },
} as const;
