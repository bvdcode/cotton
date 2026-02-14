import { create } from "zustand";
import type { User } from "../../features/auth/types";
import type { ThemeMode } from "../theme";
import type { InterfaceLayoutType } from "../api/layoutsApi";
import {
  userPreferencesApi,
  type UserPreferences,
} from "../api/userPreferencesApi";

export const USER_PREFERENCE_KEYS = {
  themeMode: "themeMode",
  editorModes: "editorModes",
  languageOverrides: "languageOverrides",

  filesLayoutType: "filesLayoutType",
  trashLayoutType: "trashLayoutType",
  filesTilesSize: "filesTilesSize",
  trashTilesSize: "trashTilesSize",

  notificationSoundEnabled: "notificationSoundEnabled",
  notificationsShowOnlyUnread: "notificationsShowOnlyUnread",

  shareLinkExpireAfterMinutes: "shareLinkExpireAfterMinutes",
} as const;

const DEFAULT_SHARE_LINK_EXPIRE_AFTER_MINUTES = 60 * 24 * 30;
const DEFAULT_THEME_MODE: ThemeMode = "system";

const DEFAULT_FILES_TILES_SIZE = "medium" as const;
const DEFAULT_TRASH_TILES_SIZE = "medium" as const;
type TilesSize = "small" | "medium" | "large";

const DEFAULT_NOTIFICATION_SOUND_ENABLED = true;
const DEFAULT_NOTIFICATIONS_SHOW_ONLY_UNREAD = false;

const parseBoolPreference = (value: string | undefined): boolean | null => {
  if (!value) return null;
  if (value === "true") return true;
  if (value === "false") return false;
  return null;
};

const parseThemeModePreference = (value: string | undefined): ThemeMode => {
  if (value === "light" || value === "dark" || value === "system") {
    return value;
  }
  return DEFAULT_THEME_MODE;
};

const parseTilesSizePreference = (value: string | undefined): TilesSize => {
  if (value === "small" || value === "medium" || value === "large") {
    return value;
  }
  return DEFAULT_FILES_TILES_SIZE;
};

const isRecordStringString = (value: object): value is Record<string, string> => {
  return !Array.isArray(value);
};

const tryParseStringRecord = (value: string | undefined): Record<string, string> => {
  if (!value) return {};
  try {
    const parsed = JSON.parse(value) as object;
    if (!parsed || typeof parsed !== "object") return {};
    if (!isRecordStringString(parsed)) return {};

    const out: Record<string, string> = {};
    for (const [key, raw] of Object.entries(parsed)) {
      if (typeof raw === "string") {
        out[key] = raw;
      }
    }
    return out;
  } catch {
    return {};
  }
};

const tryParseNumberRecord = (value: string | undefined): Record<string, number> => {
  if (!value) return {};
  try {
    const parsed = JSON.parse(value) as object;
    if (!parsed || typeof parsed !== "object") return {};
    if (Array.isArray(parsed)) return {};

    const out: Record<string, number> = {};
    for (const [key, raw] of Object.entries(parsed)) {
      if (typeof raw === "number" && Number.isFinite(raw)) {
        out[key] = raw;
      }
    }
    return out;
  } catch {
    return {};
  }
};

const parseIntPreference = (value: string | undefined): number | null => {
  if (!value) return null;
  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) ? parsed : null;
};

interface UserPreferencesState {
  preferences: UserPreferences;
  loaded: boolean;
  syncing: boolean;
  hydrateFromUser: (user: User | null) => void;
  hydrateFromRemote: (preferences: UserPreferences) => void;
  updatePreferences: (patch: UserPreferences) => Promise<void>;

  setThemeMode: (mode: ThemeMode) => void;

  setFilesLayoutType: (layoutType: InterfaceLayoutType) => void;
  setTrashLayoutType: (layoutType: InterfaceLayoutType) => void;
  setFilesTilesSize: (size: TilesSize) => void;
  setTrashTilesSize: (size: TilesSize) => void;

  setNotificationSoundEnabled: (enabled: boolean) => void;
  setNotificationsShowOnlyUnread: (showOnlyUnread: boolean) => void;

  setShareLinkExpireAfterMinutes: (expireAfterMinutes: number) => void;

  setEditorMode: (fileId: string, mode: string) => void;
  setLanguageOverride: (fileId: string, language: string) => void;
  removeLanguageOverride: (fileId: string) => void;

  reset: () => void;
}

export const useUserPreferencesStore = create<UserPreferencesState>()(
  (set, get) => ({
    preferences: {},
    loaded: false,
    syncing: false,

    hydrateFromUser: (user) => {
      if (!user?.preferences) return;
      if (get().syncing) return;
      set({ preferences: user.preferences, loaded: true });
    },

    hydrateFromRemote: (preferences) => {
      if (get().syncing) return;
      set({ preferences, loaded: true });
    },

    updatePreferences: async (patch) => {
      const previous = get().preferences;
      const optimistic: UserPreferences = { ...previous, ...patch };

      set({ preferences: optimistic, syncing: true });

      try {
        const next = await userPreferencesApi.update(patch);
        set({ preferences: next, loaded: true, syncing: false });
      } catch {
        set({ preferences: previous, syncing: false });
      }
    },

    setThemeMode: (mode) => {
      void get().updatePreferences({
        [USER_PREFERENCE_KEYS.themeMode]: mode,
      });
    },

    setFilesLayoutType: (layoutType) => {
      void get().updatePreferences({
        [USER_PREFERENCE_KEYS.filesLayoutType]: `${layoutType}`,
      });
    },

    setTrashLayoutType: (layoutType) => {
      void get().updatePreferences({
        [USER_PREFERENCE_KEYS.trashLayoutType]: `${layoutType}`,
      });
    },

    setFilesTilesSize: (size) => {
      void get().updatePreferences({
        [USER_PREFERENCE_KEYS.filesTilesSize]: size,
      });
    },

    setTrashTilesSize: (size) => {
      void get().updatePreferences({
        [USER_PREFERENCE_KEYS.trashTilesSize]: size,
      });
    },

    setNotificationSoundEnabled: (enabled) => {
      void get().updatePreferences({
        [USER_PREFERENCE_KEYS.notificationSoundEnabled]:
          enabled ? "true" : "false",
      });
    },

    setNotificationsShowOnlyUnread: (showOnlyUnread) => {
      void get().updatePreferences({
        [USER_PREFERENCE_KEYS.notificationsShowOnlyUnread]:
          showOnlyUnread ? "true" : "false",
      });
    },

    setShareLinkExpireAfterMinutes: (expireAfterMinutes) => {
      void get().updatePreferences({
        [USER_PREFERENCE_KEYS.shareLinkExpireAfterMinutes]: `${expireAfterMinutes}`,
      });
    },

    setEditorMode: (fileId, mode) => {
      const existing = selectEditorModes(get());
      const next = { ...existing, [fileId]: mode };
      void get().updatePreferences({
        [USER_PREFERENCE_KEYS.editorModes]: JSON.stringify(next),
      });
    },

    setLanguageOverride: (fileId, language) => {
      const existing = selectLanguageOverrides(get());
      const next = { ...existing, [fileId]: language };
      void get().updatePreferences({
        [USER_PREFERENCE_KEYS.languageOverrides]: JSON.stringify(next),
      });
    },

    removeLanguageOverride: (fileId) => {
      const existing = selectLanguageOverrides(get());
      if (!existing[fileId]) return;
      const { [fileId]: _removed, ...rest } = existing;
      void get().updatePreferences({
        [USER_PREFERENCE_KEYS.languageOverrides]: JSON.stringify(rest),
      });
    },

    reset: () => set({ preferences: {}, loaded: false, syncing: false }),
  }),
);

export const selectThemeMode = (state: UserPreferencesState): ThemeMode => {
  return parseThemeModePreference(state.preferences[USER_PREFERENCE_KEYS.themeMode]);
};

export const selectFilesLayoutType = (
  state: UserPreferencesState,
): InterfaceLayoutType | null => {
  const raw = state.preferences[USER_PREFERENCE_KEYS.filesLayoutType];
  const parsed = parseIntPreference(raw);
  return parsed === null ? null : (parsed as InterfaceLayoutType);
};

export const selectTrashLayoutType = (
  state: UserPreferencesState,
): InterfaceLayoutType | null => {
  const raw = state.preferences[USER_PREFERENCE_KEYS.trashLayoutType];
  const parsed = parseIntPreference(raw);
  return parsed === null ? null : (parsed as InterfaceLayoutType);
};

export const selectFilesTilesSize = (state: UserPreferencesState): TilesSize => {
  const raw = state.preferences[USER_PREFERENCE_KEYS.filesTilesSize];
  const parsed = parseTilesSizePreference(raw);
  return parsed ?? DEFAULT_FILES_TILES_SIZE;
};

export const selectTrashTilesSize = (state: UserPreferencesState): TilesSize => {
  const raw = state.preferences[USER_PREFERENCE_KEYS.trashTilesSize];
  const parsed = parseTilesSizePreference(raw);
  return parsed ?? DEFAULT_TRASH_TILES_SIZE;
};

export const selectNotificationSoundEnabled = (
  state: UserPreferencesState,
): boolean => {
  const raw = state.preferences[USER_PREFERENCE_KEYS.notificationSoundEnabled];
  return parseBoolPreference(raw) ?? DEFAULT_NOTIFICATION_SOUND_ENABLED;
};

export const selectNotificationsShowOnlyUnread = (
  state: UserPreferencesState,
): boolean => {
  const raw = state.preferences[USER_PREFERENCE_KEYS.notificationsShowOnlyUnread];
  return (
    parseBoolPreference(raw) ?? DEFAULT_NOTIFICATIONS_SHOW_ONLY_UNREAD
  );
};

export const selectEditorModes = (
  state: UserPreferencesState,
): Record<string, string> => {
  const raw = state.preferences[USER_PREFERENCE_KEYS.editorModes];
  return tryParseStringRecord(raw);
};

export const selectLanguageOverrides = (
  state: UserPreferencesState,
): Record<string, string> => {
  const raw = state.preferences[USER_PREFERENCE_KEYS.languageOverrides];
  return tryParseStringRecord(raw);
};

export const selectFileListColumnWidths = (
  state: UserPreferencesState,
): Record<string, number> => {
  const raw = state.preferences["fileListColumnWidths"];
  return tryParseNumberRecord(raw);
};

export const selectShareLinkExpireAfterMinutes = (
  state: UserPreferencesState,
): number => {
  const raw = state.preferences[USER_PREFERENCE_KEYS.shareLinkExpireAfterMinutes];
  return (
    parseIntPreference(raw) ?? DEFAULT_SHARE_LINK_EXPIRE_AFTER_MINUTES
  );
};
