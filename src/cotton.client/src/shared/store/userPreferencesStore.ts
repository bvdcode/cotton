import { create } from "zustand";
import type { User } from "../../features/auth/types";
import type { ThemeMode } from "../theme";
import { supportedLanguages, type SupportedLanguage } from "../../locales";
import {
  isSelfPreferenceUpdateToken,
  userPreferencesApi,
  type UserPreferences,
} from "../api/userPreferencesApi";

export const isSelfUpdateToken = (token: string): boolean =>
  isSelfPreferenceUpdateToken(token);

export const USER_PREFERENCE_KEYS = {
  themeMode: "themeMode",
  uiLanguage: "uiLanguage",

  notificationSoundEnabled: "notificationSoundEnabled",
  notificationsShowOnlyUnread: "notificationsShowOnlyUnread",

  shareLinkExpireAfterMinutes: "shareLinkExpireAfterMinutes",

  gallerySmoothTransitions: "gallerySmoothTransitions",
  galleryPreferPreview: "galleryPreferPreview",

  clientEncryptionLockOnRefresh: "clientEncryptionLockOnRefresh",

  searchHistory: "searchHistory",

  dashboardLayout: "dashboardLayout",
  dashboardPinnedFolderIds: "dashboardPinnedFolderIds",
} as const;

const DEFAULT_SHARE_LINK_EXPIRE_AFTER_MINUTES = 60 * 24 * 30;
const DEFAULT_THEME_MODE: ThemeMode = "system";

const DEFAULT_NOTIFICATION_SOUND_ENABLED = true;
const DEFAULT_NOTIFICATIONS_SHOW_ONLY_UNREAD = false;
const DEFAULT_GALLERY_SMOOTH_TRANSITIONS = true;
const DEFAULT_GALLERY_PREFER_PREVIEW = true;
const DEFAULT_CLIENT_ENCRYPTION_LOCK_ON_REFRESH = false;

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

  setGallerySmoothTransitions: (enabled: boolean) => void;
  setGalleryPreferPreview: (enabled: boolean) => void;
  setClientEncryptionLockOnRefresh: (enabled: boolean) => void;

  reset: () => void;
}

interface ActivePreferencesSync {
  generation: number;
  promise: Promise<void>;
}

const hasPreferenceEntries = (preferences: UserPreferences): boolean =>
  Object.keys(preferences).length > 0;

export const useUserPreferencesStore = create<UserPreferencesState>()((
  set,
  get,
) => {
  let confirmedPreferences: UserPreferences = {};
  let pendingPatch: UserPreferences = {};
  let activeSync: ActivePreferencesSync | null = null;
  let syncGeneration = 0;

  const flushPendingPatches = async (generation: number): Promise<void> => {
    while (
      generation === syncGeneration &&
      hasPreferenceEntries(pendingPatch)
    ) {
      const patch = pendingPatch;
      pendingPatch = {};

      try {
        const next = await userPreferencesApi.update(patch);
        if (generation !== syncGeneration) {
          return;
        }

        confirmedPreferences = next;
      } catch {
        if (generation !== syncGeneration) {
          return;
        }
      }

      set({
        preferences: { ...confirmedPreferences, ...pendingPatch },
        loaded: true,
        syncing: true,
      });
    }
  };

  const ensurePreferencesSync = (): Promise<void> => {
    if (activeSync?.generation === syncGeneration) {
      return activeSync.promise;
    }

    const generation = syncGeneration;
    const promise = flushPendingPatches(generation);
    activeSync = { generation, promise };

    const finish = (): void => {
      if (activeSync?.promise !== promise) {
        return;
      }

      activeSync = null;
      if (generation !== syncGeneration) {
        return;
      }

      if (hasPreferenceEntries(pendingPatch)) {
        void ensurePreferencesSync();
        return;
      }

      set({ syncing: false });
    };
    void promise.then(finish, finish);
    return promise;
  };

  return {
    preferences: {},
    loaded: false,
    syncing: false,

    hydrateFromUser: (user) => {
      if (!user?.preferences) return;
      if (get().syncing) return;
      confirmedPreferences = { ...user.preferences };
      set({ preferences: confirmedPreferences, loaded: true });
    },

    hydrateFromRemote: (preferences) => {
      if (get().syncing) return;
      confirmedPreferences = { ...preferences };
      set({ preferences: confirmedPreferences, loaded: true });
    },

    updatePreferences: (patch) => {
      if (!hasPreferenceEntries(patch)) {
        return Promise.resolve();
      }

      pendingPatch = { ...pendingPatch, ...patch };
      set((state) => ({
        preferences: { ...state.preferences, ...patch },
        syncing: true,
      }));
      return ensurePreferencesSync();
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

    setGallerySmoothTransitions: (enabled) => {
      void get().updatePreferences({
        [USER_PREFERENCE_KEYS.gallerySmoothTransitions]: enabled
          ? "true"
          : "false",
      });
    },

    setGalleryPreferPreview: (enabled) => {
      void get().updatePreferences({
        [USER_PREFERENCE_KEYS.galleryPreferPreview]: enabled ? "true" : "false",
      });
    },

    setClientEncryptionLockOnRefresh: (enabled) => {
      void get().updatePreferences({
        [USER_PREFERENCE_KEYS.clientEncryptionLockOnRefresh]: enabled
          ? "true"
          : "false",
      });
    },

    reset: () => {
      syncGeneration += 1;
      confirmedPreferences = {};
      pendingPatch = {};
      activeSync = null;
      set({ preferences: {}, loaded: false, syncing: false });
    },
  };
});

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

export const selectGallerySmoothTransitions = (
  state: UserPreferencesState,
): boolean => {
  const raw = state.preferences[USER_PREFERENCE_KEYS.gallerySmoothTransitions];
  return parseBoolPreference(raw) ?? DEFAULT_GALLERY_SMOOTH_TRANSITIONS;
};

export const selectGalleryPreferPreview = (
  state: UserPreferencesState,
): boolean => {
  const raw = state.preferences[USER_PREFERENCE_KEYS.galleryPreferPreview];
  return parseBoolPreference(raw) ?? DEFAULT_GALLERY_PREFER_PREVIEW;
};

export const selectClientEncryptionLockOnRefresh = (
  state: UserPreferencesState,
): boolean => {
  const raw =
    state.preferences[USER_PREFERENCE_KEYS.clientEncryptionLockOnRefresh];
  return parseBoolPreference(raw) ?? DEFAULT_CLIENT_ENCRYPTION_LOCK_ON_REFRESH;
};
