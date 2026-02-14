import { httpClient } from "./httpClient";

export type UserPreferences = Record<string, string>;

export const userPreferencesApi = {
  update: async (patch: UserPreferences): Promise<UserPreferences> => {
    const response = await httpClient.patch<UserPreferences>(
      "users/me/preferences",
      patch,
    );
    return response.data;
  },
};
