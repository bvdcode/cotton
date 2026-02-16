import { create } from "zustand";
import type { User } from "../../features/auth/types";
import type { ThemeMode } from "../theme";
import { supportedLanguages, type SupportedLanguage } from "../../locales";
import {
  userPreferencesApi,
  type UserPreferences,
} from "../api/userPreferencesApi";

/**
 * Stable session token used to identify this browser tab's preference updates.
 * Server echoes it back via SignalR so we can ignore our own broadcasts.
 */
const SESSION_TOKEN = (() => {
  const cryptoObj = globalThis.crypto;
  if (cryptoObj && "randomUUID" in cryptoObj) {
    return cryptoObj.randomUUID();
  }
  return `pref_${Date.now()}_${Math.random().toString(16).slice(2)}`;
})();

export const isSelfUpdateToken = (token: string): boolean =>
  token === SESSION_TOKEN;

export const USER_PREFERENCE_KEYS = {
  themeMode: "themeMode",
  uiLanguage: "uiLanguage",

  notificationSoundEnabled: "notificationSoundEnabled",
  notificationsShowOnlyUnread: "notificationsShowOnlyUnread",

  shareLinkExpireAfterMinutes: "shareLinkExpireAfterMinutes",
} as const;

const DEFAULT_SHARE_LINK_EXPIRE_AFTER_MINUTES = 60 * 24 * 30;
const DEFAULT_THEME_MODE: ThemeMode = "system";

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

  setNotificationSoundEnabled: (enabled: boolean) => void;
  setNotificationsShowOnlyUnread: (showOnlyUnread: boolean) => void;

  setShareLinkExpireAfterMinutes: (expireAfterMinutes: number) => void;

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
        const next = await userPreferencesApi.update(patch, { token: SESSION_TOKEN });
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

export const selectShareLinkExpireAfterMinutes = (
  state: UserPreferencesState,
): number => {
  const raw =
    state.preferences[USER_PREFERENCE_KEYS.shareLinkExpireAfterMinutes];
  return parseIntPreference(raw) ?? DEFAULT_SHARE_LINK_EXPIRE_AFTER_MINUTES;
};
