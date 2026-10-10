import { create } from "zustand";
import type { User } from "../../features/auth/types";
import type { ThemeMode } from "../theme";
import type { GalleryMetadataPosition } from "../types/galleryMetadataPosition";
import { supportedLanguages, type SupportedLanguage } from "../../locales";
import { reportClientError } from "../utils/clientDiagnostics";
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
  galleryMetadataPosition: "galleryMetadataPosition",
  galleryMetadataOpen: "galleryMetadataOpen",
  galleryMetadataDetailsExpanded: "galleryMetadataDetailsExpanded",

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
const DEFAULT_GALLERY_METADATA_POSITION: GalleryMetadataPosition = "right";
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
  setGalleryMetadataPosition: (position: GalleryMetadataPosition) => void;
  setGalleryMetadataOpen: (open: boolean) => void;
  setGalleryMetadataDetailsExpanded: (expanded: boolean) => void;
  setClientEncryptionLockOnRefresh: (enabled: boolean) => void;

  reset: () => void;
}

export const useUserPreferencesStore = create<UserPreferencesState>()((
  set,
  get,
) => {
  let updates = Promise.resolve();
  let savedPreferences: UserPreferences = {};
  let epoch = 0;
  let revision = 0;
  const setGalleryViewPreference = (
    key: (typeof USER_PREFERENCE_KEYS)[
      "galleryMetadataOpen" | "galleryMetadataDetailsExpanded"],
    value: boolean,
  ) => {
    const patch = { [key]: String(value) };
    if (get().loaded) {
      void get().updatePreferences(patch);
    } else {
      set({ preferences: { ...get().preferences, ...patch } });
    }
  };
  return {
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
      if (!get().syncing) {
        savedPreferences = get().preferences;
      }
      const updateEpoch = epoch;
      const updateRevision = ++revision;
      set({ preferences: { ...get().preferences, ...patch }, syncing: true });
      const update = updates.then(async () => {
        if (updateEpoch !== epoch) {
          return;
        }
        try {
          const next = await userPreferencesApi.update(patch);
          if (updateEpoch === epoch) {
            savedPreferences = next;
            set({ loaded: true });
          }
        } catch (failure) {
          reportClientError("Failed to save user preferences", failure);
        }
        if (updateEpoch === epoch && updateRevision === revision) {
          set({ preferences: savedPreferences, syncing: false });
        }
      });
      updates = update;
      await update;
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

    setGalleryMetadataPosition: (position) => {
      void get().updatePreferences({
        [USER_PREFERENCE_KEYS.galleryMetadataPosition]: position,
      });
    },

    setGalleryMetadataOpen: (open) => {
      setGalleryViewPreference(USER_PREFERENCE_KEYS.galleryMetadataOpen, open);
    },

    setGalleryMetadataDetailsExpanded: (expanded) => {
      setGalleryViewPreference(
        USER_PREFERENCE_KEYS.galleryMetadataDetailsExpanded,
        expanded,
      );
    },

    setClientEncryptionLockOnRefresh: (enabled) => {
      void get().updatePreferences({
        [USER_PREFERENCE_KEYS.clientEncryptionLockOnRefresh]: enabled
          ? "true"
          : "false",
      });
    },

    reset: () => {
      epoch++;
      updates = Promise.resolve();
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

export const selectGalleryMetadataPosition = (
  state: UserPreferencesState,
): GalleryMetadataPosition => {
  const position =
    state.preferences[USER_PREFERENCE_KEYS.galleryMetadataPosition];
  if (position === "left" || position === "right") {
    return position;
  }
  return DEFAULT_GALLERY_METADATA_POSITION;
};

export const selectGalleryMetadataOpen = (
  state: UserPreferencesState,
): boolean =>
  parseBoolPreference(
    state.preferences[USER_PREFERENCE_KEYS.galleryMetadataOpen],
  ) === true;

export const selectGalleryMetadataDetailsExpanded = (
  state: UserPreferencesState,
): boolean =>
  parseBoolPreference(
    state.preferences[USER_PREFERENCE_KEYS.galleryMetadataDetailsExpanded],
  ) === true;

export const selectClientEncryptionLockOnRefresh = (
  state: UserPreferencesState,
): boolean => {
  const raw =
    state.preferences[USER_PREFERENCE_KEYS.clientEncryptionLockOnRefresh];
  return parseBoolPreference(raw) ?? DEFAULT_CLIENT_ENCRYPTION_LOCK_ON_REFRESH;
};
