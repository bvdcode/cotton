import { httpClient, parseValidated } from "./httpClient";
import {
  userPreferencesSchema,
  type UserPreferences,
} from "./schemas/userPreferences";

export type { UserPreferences } from "./schemas/userPreferences";

const PREFERENCES_URL = "users/me/preferences";

const createPreferenceUpdateToken = (): string => {
  const cryptoObj = globalThis.crypto;
  if (typeof cryptoObj?.randomUUID === "function") {
    return cryptoObj.randomUUID();
  }

  return `pref_${Date.now()}_${Math.random().toString(16).slice(2)}`;
};

const PREFERENCE_UPDATE_TOKEN = createPreferenceUpdateToken();

export const isSelfPreferenceUpdateToken = (token: string): boolean =>
  token === PREFERENCE_UPDATE_TOKEN;

export const userPreferencesApi = {
  update: async (
    patch: UserPreferences,
    options?: { token?: string },
  ): Promise<UserPreferences> => {
    const token = options?.token ?? PREFERENCE_UPDATE_TOKEN;
    const response = await httpClient.patch<unknown>(
      PREFERENCES_URL,
      patch,
      { params: { token } },
    );
    return parseValidated(PREFERENCES_URL, response.data, userPreferencesSchema);
  },
};
