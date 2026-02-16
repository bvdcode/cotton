import { create } from "zustand";
import type { User } from "../../features/auth/types";
import type { ThemeMode } from "../theme";
import type { InterfaceLayoutType } from "../api/layoutsApi";
import { supportedLanguages, type SupportedLanguage } from "../../locales";
import {
  userPreferencesApi,
  type UserPreferences,
} from "../api/userPreferencesApi";

const SELF_UPDATE_TOKEN_TTL_MS = 30_000;
const selfUpdateTokens = new Map<string, number>();

const pruneSelfUpdateTokens = (nowMs: number): void => {
  for (const [token, expiresAtMs] of selfUpdateTokens) {
    if (expiresAtMs <= nowMs) {
      selfUpdateTokens.delete(token);
    }
  }
};

const registerSelfUpdateToken = (token: string): void => {
  const nowMs = Date.now();
  pruneSelfUpdateTokens(nowMs);
  selfUpdateTokens.set(token, nowMs + SELF_UPDATE_TOKEN_TTL_MS);
};

export const isSelfUpdateToken = (token: string): boolean => {
  const nowMs = Date.now();
  pruneSelfUpdateTokens(nowMs);

  const expiresAtMs = selfUpdateTokens.get(token);
  if (!expiresAtMs) return false;
  if (expiresAtMs <= nowMs) {
    selfUpdateTokens.delete(token);
    return false;
  }
  return true;
};

const createSelfUpdateToken = (): string => {
  const cryptoObj = globalThis.crypto;
  if (cryptoObj && "randomUUID" in cryptoObj) {
    return cryptoObj.randomUUID();
  }

  // Fallback: good enough for correlating request/echo within a single session.
  return `pref_${Date.now()}_${Math.random().toString(16).slice(2)}`;
};

export const USER_PREFERENCE_KEYS = {
  themeMode: "themeMode",
  uiLanguage: "uiLanguage",

  filesLayoutType: "filesLayoutType",
  trashLayoutType: "trashLayoutType",
  filesTilesSize: "filesTilesSize",
  trashTilesSize: "trashTilesSize",

  notificationSoundEnabled: "notificationSoundEnabled",
  notificationsShowOnlyUnread: "notificationsShowOnlyUnread",

  shareLinkExpireAfterMinutes: "shareLinkExpireAfterMinutes",
} as const;

export const USER_PREFERENCE_PREFIXES = {
  editorMode: "editorMode.",
  languageOverride: "languageOverride.",
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

const parseUiLanguagePreference = (
  value: string | undefined,
): SupportedLanguage | null => {
  if (!value) return null;
  return supportedLanguages.includes(value)
    ? (value as SupportedLanguage)
    : null;
};

const parseTilesSizePreference = (value: string | undefined): TilesSize => {
  if (value === "small" || value === "medium" || value === "large") {
    return value;
  }
  return DEFAULT_FILES_TILES_SIZE;
};

const tryParseNumberRecord = (
  value: string | undefined,
): Record<string, number> => {
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
  setUiLanguage: (language: SupportedLanguage) => void;

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
        const token = createSelfUpdateToken();
        registerSelfUpdateToken(token);
        const next = await userPreferencesApi.update(patch, { token });
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

    setUiLanguage: (language) => {
      void get().updatePreferences({
        [USER_PREFERENCE_KEYS.uiLanguage]: language,
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
        [USER_PREFERENCE_KEYS.notificationSoundEnabled]: enabled
          ? "true"
          : "false",
      });
    },

    setNotificationsShowOnlyUnread: (showOnlyUnread) => {
      void get().updatePreferences({
        [USER_PREFERENCE_KEYS.notificationsShowOnlyUnread]: showOnlyUnread
          ? "true"
          : "false",
      });
    },

    setShareLinkExpireAfterMinutes: (expireAfterMinutes) => {
      void get().updatePreferences({
        [USER_PREFERENCE_KEYS.shareLinkExpireAfterMinutes]: `${expireAfterMinutes}`,
      });
    },

    setEditorMode: (fileId, mode) => {
      void get().updatePreferences({
        [`${USER_PREFERENCE_PREFIXES.editorMode}${fileId}`]: mode,
      });
    },

    setLanguageOverride: (fileId, language) => {
      void get().updatePreferences({
        [`${USER_PREFERENCE_PREFIXES.languageOverride}${fileId}`]: language,
      });
    },

    removeLanguageOverride: (fileId) => {
      void get().updatePreferences({
        // Backend patch doesn't support deletes (Dictionary<string,string>).
        // Use empty string as a tombstone; selectors treat it as "no override".
        [`${USER_PREFERENCE_PREFIXES.languageOverride}${fileId}`]: "",
      });
    },

    reset: () => set({ preferences: {}, loaded: false, syncing: false }),
  }),
);

export const selectThemeMode = (state: UserPreferencesState): ThemeMode => {
  return parseThemeModePreference(
    state.preferences[USER_PREFERENCE_KEYS.themeMode],
  );
};

export const selectUiLanguage = (
  state: UserPreferencesState,
): SupportedLanguage | null => {
  return parseUiLanguagePreference(
    state.preferences[USER_PREFERENCE_KEYS.uiLanguage],
  );
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

const selectPrefixedStringValues = (
  preferences: UserPreferences,
  prefix: string,
): Record<string, string | undefined> => {
  const out: Record<string, string | undefined> = {};
  for (const [key, value] of Object.entries(preferences)) {
    if (!key.startsWith(prefix)) continue;
    const id = key.slice(prefix.length);
    if (!id) continue;
    out[id] = value;
  }
  return out;
};

export const selectEditorModes = (
  state: UserPreferencesState,
): Record<string, string> => {
  // Only per-file editorMode.<fileId> keys are supported now.
  const perFile = selectPrefixedStringValues(
    state.preferences,
    USER_PREFERENCE_PREFIXES.editorMode,
  );

  const out: Record<string, string> = {};
  for (const [fileId, value] of Object.entries(perFile)) {
    if (!value || value.trim().length === 0) continue;
    out[fileId] = value;
  }

  return out;
};

export const selectLanguageOverrides = (
  state: UserPreferencesState,
): Record<string, string> => {
  // Only per-file languageOverride.<fileId> keys are supported now.
  const perFile = selectPrefixedStringValues(
    state.preferences,
    USER_PREFERENCE_PREFIXES.languageOverride,
  );

  const out: Record<string, string> = {};
  for (const [fileId, value] of Object.entries(perFile)) {
    if (!value || value.trim().length === 0) continue;
    out[fileId] = value;
  }

  return out;
};

export const selectFilesTilesSize = (
  state: UserPreferencesState,
): TilesSize => {
  const raw = state.preferences[USER_PREFERENCE_KEYS.filesTilesSize];
  const parsed = parseTilesSizePreference(raw);
  return parsed ?? DEFAULT_FILES_TILES_SIZE;
};

export const selectTrashTilesSize = (
  state: UserPreferencesState,
): TilesSize => {
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
  const raw =
    state.preferences[USER_PREFERENCE_KEYS.notificationsShowOnlyUnread];
  return parseBoolPreference(raw) ?? DEFAULT_NOTIFICATIONS_SHOW_ONLY_UNREAD;
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
  const raw =
    state.preferences[USER_PREFERENCE_KEYS.shareLinkExpireAfterMinutes];
  return parseIntPreference(raw) ?? DEFAULT_SHARE_LINK_EXPIRE_AFTER_MINUTES;
};
