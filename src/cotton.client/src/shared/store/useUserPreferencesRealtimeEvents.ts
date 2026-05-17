import { useEffect } from "react";
import { eventHub, HUB_METHODS } from "../signalr";
import { isSelfUpdateToken, useUserPreferencesStore } from "./userPreferencesStore";
import { useAuth } from "../../features/auth";
import { userPreferencesSchema } from "../api/schemas/userPreferences";
import type { UserPreferences } from "../api/userPreferencesApi";
import type { JsonValue } from "../types/json";

const isUserPreferences = (value: JsonValue): value is UserPreferences => {
  return userPreferencesSchema.safeParse(value).success;
};

const isPreferencesUpdatedArgs = (
  args: JsonValue[],
): args is [string, UserPreferences] => {
  const token = args[0];
  const preferences = args[1];
  return typeof token === "string" && isUserPreferences(preferences);
};

export function useUserPreferencesRealtimeEvents(): void {
  const { isAuthenticated } = useAuth();

  useEffect(() => {
    if (!isAuthenticated) {
      return;
    }

    eventHub.start().catch(() => {
      // connection will retry automatically
    });

    const unsubscribe = eventHub.on(HUB_METHODS.PreferencesUpdated, (...args: JsonValue[]) => {
      if (!isPreferencesUpdatedArgs(args)) {
        return;
      }

      const [token, preferences] = args;
      if (isSelfUpdateToken(token)) {
        return;
      }

      useUserPreferencesStore.getState().hydrateFromRemote(preferences);
    });

    return () => {
      unsubscribe();
    };
  }, [isAuthenticated]);
}
