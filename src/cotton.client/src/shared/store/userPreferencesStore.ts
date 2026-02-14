import { create } from "zustand";
import type { User } from "../../features/auth/types";
import {
  userPreferencesApi,
  type UserPreferences,
} from "../api/userPreferencesApi";

export const USER_PREFERENCE_KEYS = {
  shareLinkExpireAfterMinutes: "shareLinkExpireAfterMinutes",
} as const;

const DEFAULT_SHARE_LINK_EXPIRE_AFTER_MINUTES = 60 * 24 * 30;

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

    reset: () => set({ preferences: {}, loaded: false, syncing: false }),
  }),
);

export const selectShareLinkExpireAfterMinutes = (
  state: UserPreferencesState,
): number => {
  const raw = state.preferences[USER_PREFERENCE_KEYS.shareLinkExpireAfterMinutes];
  return (
    parseIntPreference(raw) ?? DEFAULT_SHARE_LINK_EXPIRE_AFTER_MINUTES
  );
};
