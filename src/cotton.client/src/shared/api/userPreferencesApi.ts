import { httpClient } from "./httpClient";

export type UserPreferences = Record<string, string>;

export const userPreferencesApi = {
  update: async (
    patch: UserPreferences,
    options?: { token?: string },
  ): Promise<UserPreferences> => {
    const response = await httpClient.patch<UserPreferences>(
      "users/me/preferences",
      patch,
      options?.token ? { params: { token: options.token } } : undefined,
    );
    return response.data;
  },
};
