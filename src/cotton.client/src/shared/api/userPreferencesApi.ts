import { httpClient, parseValidated } from "./httpClient";
import {
  userPreferencesSchema,
  type UserPreferences,
} from "./schemas/userPreferences";

export type { UserPreferences } from "./schemas/userPreferences";

const PREFERENCES_URL = "users/me/preferences";

export const userPreferencesApi = {
  update: async (
    patch: UserPreferences,
    options?: { token?: string },
  ): Promise<UserPreferences> => {
    const response = await httpClient.patch<unknown>(
      PREFERENCES_URL,
      patch,
      options?.token ? { params: { token: options.token } } : undefined,
    );
    return parseValidated(PREFERENCES_URL, response.data, userPreferencesSchema);
  },
};
